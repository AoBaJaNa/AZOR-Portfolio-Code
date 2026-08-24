using System.Collections;
using UnityEngine;

public class WallClipping : MonoBehaviour
{
    internal MaterialPropertyBlock mpb;
    private Renderer renderer;
    private MeshRenderer mesh_Renderer;

    internal Vector2 tiling = new Vector2(1, 1);
    internal Vector2 offset;
    internal Vector2 Ratio;

    private Coroutine alphaCoroutine;
    private float currentAlpha = 1f;

    GameObject cutMesh;

    void Awake()
    {
        renderer = GetComponent<Renderer>();
        mesh_Renderer = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();

        if (transform.childCount > 0)
        {
            cutMesh = transform.GetChild(0).gameObject;
            cutMesh.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"WallClipping: cut mesh child is missing on {name}. Clipping fallback will use main renderer only.", this);
        }
    }

    public void ApplyProperties()
    {
        mpb.SetVector("_Range_Ratio", Ratio);
        mpb.SetVector("_Tiling", tiling);
        mpb.SetVector("_Tiling_Offset", offset);
        mpb.SetFloat("_Alpha", currentAlpha);
        renderer.SetPropertyBlock(mpb);
    }

    public void SetAlpha(float targetAlpha, float speed)
    {
        if (alphaCoroutine != null)
            StopCoroutine(alphaCoroutine);

        alphaCoroutine = StartCoroutine(AnimateAlpha(targetAlpha, speed));
    }

    private IEnumerator AnimateAlpha(float targetAlpha, float speed)
    {
        bool cutMeshActivated = cutMesh != null && cutMesh.activeSelf;

        // Main mesh visibility still drives the base wall fade.
        bool rendererEnabled = renderer.enabled;

        while (!Mathf.Approximately(currentAlpha, targetAlpha))
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * speed);

            if (targetAlpha == 0 && !cutMeshActivated)
            {
                if (cutMesh != null)
                    cutMesh.SetActive(true);

                cutMeshActivated = true;
            }

            if (targetAlpha == 1 && currentAlpha > 0.9f && cutMeshActivated)
            {
                if (cutMesh != null)
                    cutMesh.SetActive(false);

                cutMeshActivated = false;
            }

            if (currentAlpha <= 0.01f && rendererEnabled)
            {
                renderer.enabled = false;
                rendererEnabled = false;
            }

            if (currentAlpha >= 0.02f && !rendererEnabled)
            {
                renderer.enabled = true;
                rendererEnabled = true;
            }

            ApplyProperties();
            yield return null;
        }

        if (targetAlpha == 0)
        {
            if (cutMesh != null)
                cutMesh.SetActive(true);

            renderer.enabled = false;
        }
        else
        {
            if (cutMesh != null)
                cutMesh.SetActive(false);

            renderer.enabled = true;
        }
    }
}

