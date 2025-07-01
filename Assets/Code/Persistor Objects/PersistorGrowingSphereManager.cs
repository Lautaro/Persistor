using UnityEngine;
using System.Linq;
using Sirenix.OdinInspector;
using PersistorEngine;

public class PersistorGrowingSphereManager : MonoBehaviour
{
    public PersistorGrowingSphere rootSphere; // Assign in inspector
    private GameObject spherePrefab;

    void Start()
    {
        // Clone the root as a hidden prefab (without GrowingSphereManager)
        spherePrefab = Instantiate(rootSphere.gameObject);
        spherePrefab.SetActive(false);
        DestroyImmediate(spherePrefab.GetComponent<PersistorGrowingSphereManager>());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnSphere();
        }
    }

    [Button]
    void Save()
    {
        Persistor.SaveAll("Spheres");

    }

    [Button]
    void Load()
    {
        Persistor.LoadAll("Spheres");
    }

    void SpawnSphere()
    {
        // Exclude the prefab from possible parents
        var allSpheres = PersistorGrowingSphere.AllSpheres
            .Where(s => s.gameObject != spherePrefab && s.Level >= 0 && s.Level < PersistorGrowingSphere.MaxLevel)
            .ToList();

        if (allSpheres.Count == 0) return;

        // Try to find a parent that is not the one with the most children
        const int maxTries = 100;
        PersistorGrowingSphere parent = null;
        for (int attempt = 0; attempt < maxTries; attempt++)
        {
            var candidate = allSpheres[Random.Range(0, allSpheres.Count)];
            int minChildren = allSpheres.Min(s => s.Children.Count);
            if (candidate.Children.Count == minChildren)
            {
                parent = candidate;
                break;
            }
            // If not, try again
        }
        // If after maxTries we didn't find a min-child parent, just pick the one with the fewest children
        if (parent == null)
        {
            int minChildren = allSpheres.Min(s => s.Children.Count);
            var minChildParents = allSpheres.Where(s => s.Children.Count == minChildren).ToList();
            parent = minChildParents[Random.Range(0, minChildParents.Count)];
        }

        // Instantiate new sprite as child of parent (active by default)
        var newObj = Instantiate(spherePrefab, parent.transform);
        newObj.SetActive(true);

        // Copy transform (except position and rotation, which will be set below)
        newObj.transform.localScale = parent.transform.localScale;

        // Register as child and get PersistorGrowingSphere
        var newSphere = newObj.GetComponent<PersistorGrowingSphere>();
        parent.Children.Add(newSphere);

        // Set the child's level
        newSphere.Level = parent.Level + 1;

        // Copy parent's orbitRotationZ, then mutate
        newSphere.orbitRotationZ = parent.orbitRotationZ;
        newSphere.Mutate();

        // Apply the mutated orbit rotation to the child's local rotation
        newObj.transform.localRotation = Quaternion.Euler(0, 0, newSphere.orbitRotationZ);

        // Place new spawn exactly on the border of the parent
        float parentRadius = 0.5f * parent.transform.localScale.x;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * parentRadius;
        newObj.transform.localPosition = parent.transform.localPosition + offset;

        // Copy color using MaterialPropertyBlock
        var parentRenderer = parent.GetComponentInChildren<SpriteRenderer>();
        var newRenderer = newObj.GetComponentInChildren<SpriteRenderer>();
        if (parentRenderer != null && newRenderer != null)
        {
            var parentBlock = new MaterialPropertyBlock();
            parentRenderer.GetPropertyBlock(parentBlock);
            Color c = parentBlock.GetColor("_Color");
            if (c == default) c = parentRenderer.color;
            var newBlock = new MaterialPropertyBlock();
            newBlock.SetColor("_Color", c);
            newRenderer.SetPropertyBlock(newBlock);
        }
    }
}
