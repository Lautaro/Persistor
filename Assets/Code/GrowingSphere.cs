using System.Collections.Generic;
using UnityEngine;

public class GrowingSphere : MonoBehaviour
{
    public float orbitRotationZ = 4f; // degrees per second
    public const float MaxOrbitRotation = 30f; // maximum allowed rotation speed in either direction
    public static List<GrowingSphere> AllSpheres { get; } = new List<GrowingSphere>();
    public List<GrowingSphere> Children { get; } = new List<GrowingSphere>();
    public int Level { get; set; } = 0;
    public const int MaxLevel = 7;
    public float ColorMutationStrength; 

    void Awake()
    {
        AllSpheres.Add(this);

        // Ensure the sprite renderer uses a compatible material (Unlit/Color)
        var rend = GetComponentInChildren<SpriteRenderer>();
        if (rend != null)
        {
            var mat = rend.sharedMaterial;
            if (mat == null || !mat.HasProperty("_Color"))
            {
                var compatibleMat = new Material(Shader.Find("Sprites/Default"));
                compatibleMat.color = Color.white;
                rend.sharedMaterial = compatibleMat;
            }

            // Set sortingOrder to be one higher than parent's, or 0 if root
            int parentOrder = 0;
            var parentSphere = transform.parent ? transform.parent.GetComponentInParent<GrowingSphere>() : null;
            if (parentSphere != null && parentSphere != this)
            {
                var parentRend = parentSphere.GetComponentInChildren<SpriteRenderer>();
                if (parentRend != null)
                    parentOrder = parentRend.sortingOrder + 1;
            }
            rend.sortingOrder = parentOrder;

            // If this is the root sphere, randomize its color
            if (parentSphere == null || Level == 0)
            {
                rend.color = new Color(Random.value, Random.value, Random.value, 1f);
            }
        }
    }

    void OnDestroy()
    {
        AllSpheres.Remove(this);
        var parent = transform.parent ? transform.parent.GetComponentInParent<GrowingSphere>() : null;
        if (parent != null)
            parent.Children.Remove(this);
    }

    void Update()
    {
        // Rotate around Z axis if orbitRotationZ is not zero (degrees per second)
        if (Mathf.Abs(orbitRotationZ) > 0.001f)
        {
            transform.Rotate(0f, 0f, orbitRotationZ * Time.deltaTime, Space.Self);
        }
    }

    public void Mutate()
    {
        // Scale: always slightly smaller than parent (no randomness)
        float shrinkFactor = 0.96f;
        float newScale = Mathf.Clamp(transform.localScale.x * shrinkFactor, 0.1f, 10f);
        transform.localScale = new Vector3(newScale, newScale, 1f);

        // Mutate color directly on SpriteRenderer (much stronger mutation)
        var rend = GetComponentInChildren<SpriteRenderer>();
        if (rend != null)
        {
            Color c = rend.color;
            c.r = Mathf.Clamp01(c.r + Random.Range(-ColorMutationStrength, ColorMutationStrength));
            c.g = Mathf.Clamp01(c.g + Random.Range(-ColorMutationStrength, ColorMutationStrength));
            c.b = Mathf.Clamp01(c.b + Random.Range(-ColorMutationStrength, ColorMutationStrength));
            rend.color = c;
        }

        // Mutate ColorMutationStrength (min 0, max 0.5f)
        //ColorMutationStrength = Mathf.Clamp(
        //    ColorMutationStrength + Random.Range(-0.02f, 0.02f),
        //    0f, 0.3f);

        // Mutate orbit rotation (slowly, between -MaxOrbitRotation and +MaxOrbitRotation)
        float mutationStep = Random.Range(-10f, 10f); // degrees per generation, not very fast
        orbitRotationZ = Mathf.Clamp(orbitRotationZ + mutationStep, -MaxOrbitRotation, MaxOrbitRotation);
    }

    // Helper to set sorting order based on depth in hierarchy
    public void SetSortingOrderRecursive(int baseOrder)
    {
        var rend = GetComponentInChildren<SpriteRenderer>();
        if (rend != null)
            rend.sortingOrder = baseOrder;

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].SetSortingOrderRecursive(baseOrder + 1);
        }
    }
}
