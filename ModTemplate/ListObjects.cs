using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace ModTemplate;

public class ListObjects : MonoBehaviour
{
    private const float Margin = 0.5f;
    private readonly HashSet<long> _usedIds = new();
    private readonly int _renderLayer = 3;
    private readonly ObjectsJson _objectsJson = new();
    private Camera _pictureCamera;
    private RenderTexture _renderTexture;
    private string _pictureFolderPath;
    private int _fileIndex;

    [JsonObject(MemberSerialization.OptOut)]
    private class ObjectInfo
    {
        public string Path;
        public string MeshName;
    }
    
    [JsonObject(MemberSerialization.OptOut)]
    private class ObjectsJson
    {
        public Dictionary<string, string> Objects = new();
    }

    private void Start()
    {
        _renderTexture = new RenderTexture(256, 256, 0);
        
        _pictureCamera = new GameObject("PicCamera").AddComponent<Camera>();
        _pictureCamera.orthographic = true;
        _pictureCamera.orthographicSize = 1;
        _pictureCamera.targetDisplay = -1;
        _pictureCamera.enabled = false;
        _pictureCamera.nearClipPlane = 0;
        _pictureCamera.targetTexture = _renderTexture;
        _pictureCamera.clearFlags = CameraClearFlags.Color;
        _pictureCamera.backgroundColor = Color.white;
        _pictureCamera.cullingMask = 1 << _renderLayer;

        var cameraLight = _pictureCamera.gameObject.AddComponent<Light>();
        cameraLight.type = LightType.Point;
        cameraLight.range = 1000;

        _pictureFolderPath = Application.dataPath + "/ObjectPics";
        ModTemplate.Instance.ModHelper.Console.WriteLine($"### will put pictures in {_pictureFolderPath}");
        Directory.CreateDirectory(_pictureFolderPath);

        StartCoroutine(StartTakingPics());
    }

    private IEnumerator StartTakingPics()
    {
        yield return new WaitForEndOfFrame();
        TakePics(transform, transform.name);

        File.WriteAllText(Path.Combine(_pictureFolderPath, "objects.json"), JsonConvert.SerializeObject(_objectsJson, Formatting.Indented));
    }

    private static void EnableAllParents(GameObject parentObject)
    {
        if (parentObject.activeInHierarchy) return;

        parentObject.SetActive(true);
        if (parentObject.transform.parent)
        {
            EnableAllParents(parentObject.transform.parent.gameObject);
        }
    }

    private void TakePics(Transform parent, string path)
    {
        var meshFilters = parent.GetComponentsInChildren<MeshFilter>(true);

        if (meshFilters.Length == 0) return;

        TakePic(meshFilters, path);
        
        foreach (Transform child in parent)
        {
            var childPath = $"{path}/{child.name}";
            TakePics(child, childPath);
        }
    }

    private void TakePic(IReadOnlyList<MeshFilter> meshFilters, string path)
    {
        if (meshFilters.Count == 0) return;
        
        var id = ChangeLayerAndGetId(meshFilters);
        if (id != 0 && !_usedIds.Contains(id))
        {
            _usedIds.Add(id);

            var boundResult = GetRendererListBounds(meshFilters);

            if (!boundResult.HasValue)
            {
                ModTemplate.Instance.ModHelper.Console.WriteLine($"Skipping {path}: no bounds");
                return;
            }

            var bounds = boundResult.Value;

            _pictureCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z) - Vector3.forward * Margin;
            _pictureCamera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) / 2f + Margin;
            _pictureCamera.farClipPlane = bounds.size.z + 2 * Margin;
            
            RenderTexture.active = _renderTexture;
            _pictureCamera.Render();
            
            var image = new Texture2D(_renderTexture.width, _renderTexture.height);
            image.ReadPixels(new Rect(0, 0, _renderTexture.width, _renderTexture.height), 0, 0);
            image.Apply();
     
            var bytes = image.EncodeToPNG();
            Destroy(image);

            var fileName = $"{_fileIndex.ToString()}.png";
            _fileIndex++;
            File.WriteAllBytes(Path.Combine(_pictureFolderPath, fileName), bytes);
            _objectsJson.Objects[fileName] = path;
            
            ModTemplate.Instance.ModHelper.Console.WriteLine(path);
        }
        else
        {
            ModTemplate.Instance.ModHelper.Console.WriteLine($"already included {path}, skipping");
        }
        
        HideLayers(meshFilters);
    }

    private static void HideLayers(IEnumerable<MeshFilter> meshFilters)
    {
        foreach (var meshFilter in meshFilters)
        {
            meshFilter.gameObject.layer = 0;
        }
    }

    private long ChangeLayerAndGetId(IEnumerable<MeshFilter> meshFilters)
    {
        return meshFilters.Aggregate<MeshFilter, long>(0, (current, meshFilter) =>
        {
            var renderer = meshFilter.GetComponent<Renderer>();
                
            if (!renderer || !meshFilter.sharedMesh) return 0;
            
            renderer.enabled = true;
            EnableAllParents(meshFilter.gameObject);
            meshFilter.gameObject.layer = _renderLayer;

            return current + meshFilter.sharedMesh.GetInstanceID();
        });
    }

    private static Bounds? GetRendererListBounds(IReadOnlyList<MeshFilter> meshFilters)
    {
        var firstRenderer = meshFilters.FirstOrDefault(meshFilter => meshFilter.GetComponent<Renderer>() != null)?.GetComponent<Renderer>();

        if (!firstRenderer) return null;
        
        var bounds = new Bounds(firstRenderer.bounds.center, firstRenderer.bounds.size);
        foreach (var meshFilter in meshFilters)
        {
            var renderer = meshFilter.GetComponent<Renderer>();
            if (!renderer) continue;
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }
}