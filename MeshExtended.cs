/* **********************************
 * Mesh Extensions, designed for Unity 2019.3
 * - Helper class for working with meshes
 *   
 * Author: Tom Angell, May 2020
 * All Rights Reserved
 * **********************************/


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Tom.Traditional;

public class MeshExtended
{
    /* 
     * Class to extend a mesh object with some additional features.
     */

    private Vector3[] m_adjustedVertices;  // Array-based verts, adjusted for scale, rotation, position
    private Bounds m_adjustedBounds;  // Bounds, adjusted for scale, rotation, position
    private Mesh m;
    private GameObject go;

    public MeshExtended(GameObject gameObj)
    {
        go = gameObj;

        MeshFilter mf;
        if (go.TryGetComponent(out mf))
        {
            m = mf.sharedMesh;
        } else  // No direct meshes, get all meshes in children and use first found
        {
            // Try mesh filters first, then skinned mesh renderers
            MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>();
            SkinnedMeshRenderer[] smr = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            m = (mfs.Length != 0) ? mfs[0].sharedMesh : smr[0].sharedMesh;
        }

        // Trigger changed method
        MeshChanged();
    }

    public class MeshObject
    {
        public string Name { get; }
        public int Triangles { get; }

        public MeshObject(string name, int tris)
        {
            Name = name;
            Triangles = tris;
        }
    }

    private class TriangleEdge : IEquatable<TriangleEdge>
    {
        public float Length
        {
            get
            {
                // Note since we're only using length for comparison purpose,
                // true length does not matter. sqrMag is faster for this purpose.
                return (Vertex1 - Vertex2).sqrMagnitude;
            }
        }
        public int Triangle { get; }
        public int Index1 { get; }
        public int Index2 { get; }
        public Vector3 Vertex1 { get; }
        public Vector3 Vertex2 { get; }
        public TriangleEdge(int tri, int idx1, int idx2, Vector3 vert1, Vector3 vert2)
        {
            Triangle = tri;
            Index1 = idx1;
            Index2 = idx2;
            Vertex1 = vert1;
            Vertex2 = vert2;
        }
        public bool Equals(TriangleEdge other)
        {
            // Finds triangles that share the same edge, but don't share the same triangle
            bool compare1 = SharesIndices(other);
            //bool compare2 = (Index2 == other.Index1 && Index1 == other.Index2);
            bool compare2 = (Vertex1 == other.Vertex1 && Vertex2 == other.Vertex2);
            bool compare3 = (Vertex2 == other.Vertex1 && Vertex1 == other.Vertex2);
            bool compare4 = (Triangle != other.Triangle);
            return (compare1 || compare2 || compare3) && compare4;
        }

        public bool SharesIndices(TriangleEdge other)
        {
            bool compare1 = (Index1 == other.Index1 && Index2 == other.Index2);
            bool compare2 = (Index2 == other.Index1 && Index1 == other.Index2);
            return compare1 || compare2;
        }

    }

    [Serializable]
    public class MeshFinalizationProperties
    {
        public float[] LOD_decimationPercents = new float[0];  // float for easy serialized property modification
        public int[] LOD_decimationModes = new int[0];
        public float LOD_transitionStart = 0.75f, LOD_culled = 0.10f;
        public string prefabPath = "Prefabs/LOD/obj";
        public bool destroyChildren = false;

        public MeshFinalizationProperties() { }
        public MeshFinalizationProperties(string objName)
        {
            prefabPath = prefabPath.Replace("/obj", "/" + objName);
        }
    }

    private void MeshChanged ()
    {
        if (m == null) { return; }

        if ((go.transform.position != LastPosition) ||
            (go.transform.rotation != LastRotation) ||
            (go.transform.localScale != LastScale))
        {
            LastPosition = go.transform.position;
            LastRotation = go.transform.rotation;
            LastScale = go.transform.localScale;

            // If anything about the transform has changed, recalc verts
            m.RecalculateBounds();
            m_adjustedVertices = new Vector3[m.vertices.Length];
            for (int i = 0; i < m.vertices.Length; i++)
            {
                // Modify by scale, rotation, then position
                Vector3 v = go.transform.rotation * Vector3.Scale(m.vertices[i], go.transform.localScale) + go.transform.position;
                m_adjustedVertices[i] = v;
            }

            // Also adjust bounds
            m_adjustedBounds = m.bounds;
            m_adjustedBounds.size = Vector3.Scale(m.bounds.size, go.transform.localScale);
        }
    }

    public Vector3[] Vertices
    {
        /* 
         * Return vertex position modified by current position
         */
        get
        {
            MeshChanged();
            return m_adjustedVertices;
        }
    }

    public int[] Triangles
    {
        /* 
         * Return triangles
         */

        get
        {
            return m.triangles;
        }
    }

    public Vector3[] Normals
    {
        /* 
         * Return an array of lists of normal vectors
         */

        get
        {
            MeshChanged();
            return m.normals;
        }
    }

    public Bounds Bounds
    {
        /* 
         * Return the bounds
         */

        get
        {
            MeshChanged();
            return m_adjustedBounds;
        }
    }

    public float AverageScale
    {
        /* 
         * Return average scale
         */

        get
        {
            return (go.transform.localScale.x + go.transform.localScale.y + go.transform.localScale.z) / 3f;
        }
    }

    public MeshObject[] MeshObjects
    {
        /* 
         * Return all the mesh objects that are part of this object (triangles and names)
         */
        get
        {
            List<MeshObject> mos = new List<MeshObject>();
            Component[] meshes = go.GetComponentsInChildren(typeof(MeshFilter));
            if (meshes != null)
            {
                foreach (MeshFilter mf in meshes)
                {
                    string name = (mf.sharedMesh.name == "") ? mf.gameObject.name : mf.sharedMesh.name;
                    mos.Add(new MeshObject(name, mf.sharedMesh.triangles.Length / 3));
                }
            }

            Component[] smr = go.GetComponentsInChildren(typeof(SkinnedMeshRenderer));
            if (smr != null)
            {
                foreach (SkinnedMeshRenderer smr1 in smr)
                {
                    string name = (smr1.sharedMesh.name == "") ? smr1.gameObject.name : smr1.sharedMesh.name;
                    mos.Add(new MeshObject(name, smr1.sharedMesh.triangles.Length / 3));
                }
            }
            return mos.ToArray();
        }
    }

    public GameObject FinalizeMesh(MeshFinalizationProperties fp)
    {
        /* 
         * Finalize the mesh. Steps are:
         * 1) Separate entire object into meshes flattened by material
         * 2) Decimate each material mesh and repack into decimation levels
         * 3) Combine decimation levels into single mesh
         * 4) Generate LODs using each mesh
         * Returns a reference to the new LOD GameObject if one was made
         */

        // Initialize nulls
        if (fp.LOD_decimationPercents == null) { fp.LOD_decimationPercents = new float[0]; }
        if (fp.prefabPath.Length == 0) { fp.prefabPath = null; }

        // Hold materials
        List<Material> materials = new List<Material>();

        // Generate new meshes flattened by material
        List<Mesh> materialMeshes = MeshByMaterial(out materials);

        // Initialize counts
        int numberMeshes = materialMeshes.ToArray().Length;
        int numberDecimations = fp.LOD_decimationPercents.Length / numberMeshes;

        // Initialize decimation levels
        List<Mesh>[] decimationLevels = new List<Mesh>[numberDecimations + 1];  // +1 for the original mesh
        for (int i = 0; i < decimationLevels.Length; i++)
        {
            decimationLevels[i] = new List<Mesh>();
        }

        // Decimate each material mesh and repack into decimation levels
        for (int i = 0; i < numberMeshes; i++)
        {
            Mesh mesh = materialMeshes[i];
            decimationLevels[0].Add(mesh);  // Index 0 is the original mesh
            if (fp.LOD_decimationPercents.Length > 0)  // If percents for decimation supplied, then decimate
            {
                // Slice the supplied decimation percentage parameter to get the percentages for this mesh
                float[] decimatePercents1 = new float[numberDecimations];
                for (int j = 0; j < numberDecimations; j++)
                {
                    int pointer = i * numberDecimations + j;
                    decimatePercents1[j] = fp.LOD_decimationPercents[pointer];
                }

                //Debug.Log("Material " + materials[i] + " uses percents " + string.Join(", ", decimatePercents1));

                List<Mesh> decimated = DecimateMesh(mesh, decimatePercents1, fp.LOD_decimationModes[i]);
                for (int j = 1; j < decimated.ToArray().Length + 1; j++)
                {
                    decimationLevels[j].Add(decimated.ToArray()[j-1]);
                }
            }
        }

        // Using decimation levels, rebuild the new mesh(es)
        if (decimationLevels.Length > 1)  // LOD
        {
            // Create a new game object so as not to interfere with the original
            GameObject newGo = new GameObject(go.name + "_LOD");
            newGo.transform.position = go.transform.position + new Vector3(Bounds.size.x, 0, 0);
            LODGroup lodg = newGo.AddComponent<LODGroup>();
            LOD[] lods = new LOD[decimationLevels.Length];

            // Establish LOD thresholds
            float transitionDistance = fp.LOD_transitionStart - fp.LOD_culled;

            for (int k = 0; k < decimationLevels.Length; k++)
            {
                GameObject lodGo;
                Mesh finalMesh = CombineMeshes(decimationLevels[k]);
                // Loop through, halving distance each time
                float transition = fp.LOD_culled + transitionDistance * (1f / 2);
                transition = (k == 0) ? fp.LOD_transitionStart : transition;
                transition = (k == decimationLevels.Length - 1) ? fp.LOD_culled : transition;
                transitionDistance = transition - fp.LOD_culled;

                lods[k] = TomLOD.BuildLOD(go.name + "_LOD" + k, newGo.transform.position, finalMesh, materials.ToArray(), transition, out lodGo);
                lodGo.transform.SetParent(newGo.transform);
            }

            lodg.SetLODs(lods);
            lodg.RecalculateBounds();

            return newGo;

        }
        else  // Only one mesh, no need for LOD - just combine submeshes and send back to the original object
        {
            Mesh finalMesh = CombineMeshes(decimationLevels[0]);
            // Apply the mesh and materials back
            // Null check in case the parent doesn't have either a mesh or renderer
            MeshFilter mf;
            if (!go.TryGetComponent(out mf)) { mf = go.AddComponent<MeshFilter>(); }
            mf.sharedMesh = finalMesh;
            MeshRenderer mr;
            if (!go.TryGetComponent(out mr)) { mr = go.AddComponent<MeshRenderer>(); }
            mr.sharedMaterials = materials.ToArray();

            // Destroy children based on parameter
            if (fp.destroyChildren) { Utilities.DestroyChildren(go); }
            return null;
        }
    }
    private static List<Mesh> DecimateMesh(Mesh m, float[] percents, int mode = 0)
    {
        /* 
         * Iterate through the mesh and decimate it. Lowers the poly count.
         * Concept is to find the edge(s) with shortest length, collapse
         * the edge(s) to center, and delete the two associated triangles.
         * http://paulbourke.net/geometry/polygonmesh/
         * 
         * Mode parameter determines method of decimation. 0 = decimate smallest edge lengths, 1 = decimate evenly across the mesh
         */

        // Initialize
        Vector3[] newVerts = m.vertices;
        List<int> removeTris = new List<int>();
        List<TriangleEdge> edges = new List<TriangleEdge>();
        TriangleEdge[] arrayEdges = edges.ToArray();
        List<Mesh> meshes = new List<Mesh>();

        // Calculate the breakpoints (in triangles) for each percent supplied
        int[] breakpoints = new int[percents.Length];
        for (int k = 0; k < percents.Length; k++)
        {
            breakpoints[k] = (int)((m.triangles.Length / 3) * (1f - percents[k]));
        }
        Array.Sort(breakpoints);  // sort ascending

        bool firstPass = true, stop = false;

        int i = 0, j = 0, breakpoint_pointer = 0, iReset = 20, indexBias = 0;
        // Note: i keeps track of completed collapse attempts, which then resets the edge array and j
        // j keeps track of the index in the edge array
        // breakpoint_pointer keeps track of the breakpoint mesh that we're building
        // iReset is the number of successful edge converges we have before resetting the edge array
        // indexBias is used to evenly distribute j across the edge array (based on mode)
        //DateTime st = DateTime.Now;
        while (breakpoint_pointer < breakpoints.Length && !stop)
        {
            /* Build the edge length array to determine the edges and triangles to collapse.
             * For efficiency, only rebuild the array once every so many passes (iReset).
             */
            if (i >= iReset || firstPass)
            {
                edges = new List<TriangleEdge>();
                for (int k = 0; k < m.triangles.Length; k += 3)
                {
                    int triangle = (int)Mathf.Floor(k / 3);
                    if (!removeTris.Contains(triangle))
                    {
                        int idx1 = m.triangles[k], idx2 = m.triangles[k + 1], idx3 = m.triangles[k + 2];
                        Vector3 vert1 = newVerts[idx1], vert2 = newVerts[idx2], vert3 = newVerts[idx3];

                        edges.Add(new TriangleEdge(triangle, idx1, idx2, vert1, vert2));
                        edges.Add(new TriangleEdge(triangle, idx2, idx3, vert2, vert3));
                        edges.Add(new TriangleEdge(triangle, idx1, idx3, vert1, vert3));
                    }
                }

                // Sort by smallest length
                edges.Sort(delegate (TriangleEdge e1, TriangleEdge e2) { return e1.Length.CompareTo(e2.Length); });

                arrayEdges = edges.ToArray();  // Rebuild into an array for easy indexing
                j = 0;  // Reset the pointer index
                i = 0;  // Reset the collapse counter
                // Index bias based on # of edges we're looking to converge before resetting j
                // Note + 2, for safety so we don't exceed the length of the edge array and stop the iterations
                indexBias = arrayEdges.Length / (iReset + 2);
                firstPass = false;
            }

            TriangleEdge edge = arrayEdges[j];  // Start with the smallest edge

            // Find the complement edge for this edge
            // Note on this process: IndexOf uses the Equals method of
            // the TriangleEdge class, and seems to be more performant
            // than looping through the array manually to find the complement
            bool found = false;
            int complementIdx = 0;
            complementIdx = Array.IndexOf(arrayEdges, edge, j + 1);
            found = (complementIdx != -1);

            if (found)
            {
                TriangleEdge edge1 = arrayEdges[complementIdx];
                //Debug.Log("edge " + j + " matched with edge " + complementIdx + ", triangles are " + edge.Triangle + " and " + edge1.Triangle);
                // Add the triangles to the removal list
                if (!removeTris.Contains(edge.Triangle)) { removeTris.Add(edge.Triangle); }
                if (!removeTris.Contains(edge1.Triangle)) { removeTris.Add(edge1.Triangle); }

                // Build list of verts to collapse to center
                List<Vector3> collapseVerts = new List<Vector3>();
                collapseVerts.Add(newVerts[edge.Index1]);
                collapseVerts.Add(newVerts[edge.Index2]);
                List<int> collapseIndices = new List<int>();
                collapseIndices.Add(edge.Index1);
                collapseIndices.Add(edge.Index2);
                if (!edge.SharesIndices(edge1)) // The edges may have different indices, so check
                {
                    collapseIndices.Add(edge1.Index1);
                    collapseIndices.Add(edge1.Index2);
                }

                // Check if other verts exist in the same position
                for (int k = 0; k < m.vertices.Length; k++)
                {
                    Vector3 vert = newVerts[k];
                    if (collapseVerts.Contains(vert) && !collapseIndices.Contains(k)) { collapseIndices.Add(k); }
                }

                // Update all indices with the new position
                Vector3 newPos = (edge.Vertex1 + edge.Vertex2) / 2;
                for (int k = 0; k < collapseIndices.ToArray().Length; k++)
                {
                    newVerts[collapseIndices[k]] = newPos;
                }
                i++;

                // Determine next edge to check by mode parameter
                if (mode == 0)  // Smallest edge lengths mode
                {
                    j = complementIdx + 1;  // Set the next iteration one past the complement index, since everything is sorted ascending
                } else if (mode == 1)  // Destributed across the edge array
                {
                    j = i * indexBias;  // Iterate evenly across the edge array
                }
            }
            else  // If not found, increment to the next edge
            {
                j++;
            }

            // If j reaches the end of the edge array, no other collapsible edges were found; stop the loop
            stop = (j >= arrayEdges.Length);

            //if (stop)
            //{
            //    Debug.Log("stopped on " + j);
            //}

            // After determining which triangles can be removed, and where the new verts are,
            // check for breakpoint and build a new mesh
            bool breakpoint_reached = (removeTris.ToArray().Length >= breakpoints[breakpoint_pointer]);
            if (breakpoint_reached || stop)
            {
                // Rebuild the triangle array, without the decimated triangles
                int[] newTris = new int[m.triangles.Length - (removeTris.ToArray().Length * 3)];
                int triPointer = 0;
                for (int k = 0; k < m.triangles.Length; k += 3)
                {
                    int triangle = (int)Mathf.Floor(k / 3);
                    if (!removeTris.Contains(triangle))
                    {
                        newTris[triPointer] = m.triangles[k];
                        newTris[triPointer + 1] = m.triangles[k + 1];
                        newTris[triPointer + 2] = m.triangles[k + 2];
                        triPointer += 3;
                    }
                }

                // Rebuild the vert and uv arrays, stripping out verts that are no longer needed
                List<Vector3> newVerts2 = new List<Vector3>();
                List<Vector2> newUVs = new List<Vector2>();
                List<int> removeVerts = new List<int>();
                for (int k = 0; k < newVerts.Length; k++)
                {
                    if (Array.IndexOf(newTris, k) == -1)  // this vert index doesn't exist in the triangles, don't add it to the new array
                    {
                        removeVerts.Add(k);
                    }
                    else  // the vert exists in the triangles, add it to the new array
                    {
                        newVerts2.Add(newVerts[k]);
                        newUVs.Add(m.uv[k]);  // Note that the dimension of newVerts = m.vertices = m.uv, so the k reference can be shared equally
                    }
                }

                // Adjust the triangle array, accounting for all removed vertices
                for (int k = 0; k < newTris.Length; k++)
                {
                    int count = 0;
                    foreach (int vert in removeVerts)
                    {
                        if (newTris[k] > vert) { count += 1; }  // Count how many lower indices were removed
                    }
                    newTris[k] -= count;  // Adjust the index in the triangle by the count
                }

                //Build the mesh
                Mesh newMesh = new Mesh();
                newMesh.vertices = newVerts2.ToArray();
                newMesh.uv = newUVs.ToArray();
                newMesh.triangles = newTris;
                // Recalc
                newMesh.RecalculateBounds();
                newMesh.RecalculateNormals();
                newMesh.RecalculateTangents();

                meshes.Add(newMesh);

                // If the loop stops before finishing out the breakpoints, just
                // flood the last mesh through all remaining breakpoints
                if (stop)
                {
                    while (meshes.ToArray().Length < breakpoints.Length)
                    {
                        meshes.Add(newMesh);
                    }
                }

                // Iterate the breakpoint pointer
                breakpoint_pointer += 1;
            }
        }

        //Debug.Log(DateTime.Now - st + " time elapsed");

        return meshes;
    }

    private List<Mesh> MeshByMaterial(out List<Material> materials)
    {
        /* 
         * Returns all meshes flattened by material
         * from https://www.youtube.com/watch?v=6APzUgckV7U
         */

        // Get the mesh filters for the object
        MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>();

        // Store transform information, and reset to identity
        Vector3 origPosition = go.transform.position;
        Quaternion origRotation = go.transform.rotation;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        // Get materials
        materials = Materials;

        // Go through each of the materials and find which renderers use it
        List<Mesh> meshes = new List<Mesh>();
        foreach (Material mat in materials)
        {
            List<CombineInstance> combine1 = new List<CombineInstance>();
            foreach (MeshFilter mf in mfs)
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                int i = 0;
                foreach (Material mat1 in mr.sharedMaterials)
                {
                    if (mat == mat1)  // This filter shares the same material, add this to combine instance
                    {
                        CombineInstance c = new CombineInstance();
                        c.subMeshIndex = i;
                        c.mesh = mf.sharedMesh;
                        c.transform = mf.transform.localToWorldMatrix;
                        combine1.Add(c);
                    }
                    i++;
                }
            }
            Mesh mesh1 = new Mesh();
            mesh1.CombineMeshes(combine1.ToArray(), true);  // Combine all materials with the same material into one submesh
            meshes.Add(mesh1);
        }

        // Restore transform information
        go.transform.position = origPosition;
        go.transform.rotation = origRotation;

        return meshes;
    }

    private static Mesh CombineMeshes(List<Mesh> meshes)
    {
        /* 
         * Combines the supplied meshes into a single mesh
         * from https://www.youtube.com/watch?v=6APzUgckV7U
         */

        // Combine all the supplied meshes into one overall mesh
        CombineInstance[] combine = new CombineInstance[meshes.ToArray().Length];
        for (int i = 0; i < meshes.ToArray().Length; i++)
        {
            combine[i].subMeshIndex = 0;
            combine[i].mesh = meshes[i];
            combine[i].transform = Matrix4x4.identity;
        }
        Mesh mesh = new Mesh();
        mesh.CombineMeshes(combine, false);  // Don't flatten

        return mesh;
    }

    public Vector3 LastScale { get; set; }
    public Vector3 LastPosition { get; set; }
    public Quaternion LastRotation { get; set; }

    public List<Material> Materials
    {
        /* 
         * Returns all materials used by the object
         */
        get
        {
            MeshRenderer[] mrs = go.GetComponentsInChildren<MeshRenderer>();
            // Generate list of materials from the renderers
            List<Material> materials = new List<Material>();
            foreach (MeshRenderer renderer in mrs)
            {
                Material[] mats = renderer.sharedMaterials;
                foreach (Material mat in mats)
                {
                    if (!materials.Contains(mat)) { materials.Add(mat); }
                }
            }

            return materials;
        }
    }
}
