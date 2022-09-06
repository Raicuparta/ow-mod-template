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
        _pictureCamera.backgroundColor = Color.clear;
        _pictureCamera.cullingMask = 1 << _renderLayer;

        var cameraLight = _pictureCamera.gameObject.AddComponent<Light>();
        cameraLight.type = LightType.Directional;

        _pictureFolderPath = Application.dataPath + "/ObjectPics";
        Debug.Log($"### will put pictures in {_pictureFolderPath}");
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
        var meshRenderers = parent.GetComponentsInChildren<MeshRenderer>(true);
        var skinnedRenderers = parent.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        if (meshRenderers.Length + meshRenderers.Length == 0) return;

        TakePic(meshRenderers, path);
        TakePic(skinnedRenderers, path);
        
        foreach (Transform child in parent)
        {
            var childPath = $"{path}/{child.name}";
            TakePics(child, childPath);
        }
    }

    private void TakePic(IReadOnlyList<Renderer> renderers, string path)
    {
        if (renderers.Count == 0) return;
        
        var id = ChangeLayerAndGetId(renderers);
        if (!_usedIds.Contains(id))
        {
            _usedIds.Add(id);

            var bounds = GetRendererListBounds(renderers);

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
            
            Debug.Log(path);
        }
        else
        {
            Debug.Log($"already included {path}, skipping");
        }
        
        HideLayers(renderers);
    }

    private static void HideLayers(IEnumerable<Renderer> renderers)
    {
        foreach (var r in renderers)
        {
            Debug.Log($"hiding layer of {r.gameObject.name}");
            r.gameObject.layer = 0;
        }
    }

    private long ChangeLayerAndGetId(IEnumerable<Renderer> renderers)
    {
        return renderers.Aggregate<Renderer, long>(0, (current, r) =>
        {
            Debug.Log($"setting layer of {r.gameObject.name}");
            r.enabled = true;
            EnableAllParents(r.gameObject);
            r.gameObject.layer = _renderLayer;
            return current + r.GetInstanceID();
        });
    }

    private static Bounds GetRendererListBounds(IReadOnlyList<Renderer> renderers)
    {
        var bounds = new Bounds(renderers[0].bounds.center, renderers[0].bounds.size);
        foreach (var r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        return bounds;
    }
}