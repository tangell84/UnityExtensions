/* **********************************
 * Branch Spawner, designed for Unity 2019.3
 * - A tool to spawn branch meshes on tree trunks randomly
 *   using raycasts and custom properties
 * - Finalization then compresses the tree and branches
 *   into a single mesh, flattens the material, and LODs it
 *   
 * Author: Tom Angell, April 2020
 * All Rights Reserved
 * **********************************/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Tom.Traditional;

[ExecuteInEditMode]
public class BranchSpawner : MonoBehaviour
{
    /* 
     * Class for spawning branches 
     */

    // Public variables
    public BranchSpawnerProperties branchSpawnerProperties = new BranchSpawnerProperties();
    public MeshExtended.MeshFinalizationProperties finalizationProperties = new MeshExtended.MeshFinalizationProperties();
    public bool drawDebug;
    public bool drawSpawnPositions = false;
    public int drawVert = 0;

    // Properties
    public GameObject Go { get; private set; }
    public MeshExtended MeshExt { get; private set; }
    public bool MostlyTrunk { get; private set; }
    public List<BranchSpawnLocation> SpawnLocations { get; private set; }
    public int? TreeLayer
    {
        get
        {
            // Null check
            if (Go == null)
                return null;

            return Go.layer;
        }
    }

    private LayerMask? TreeMask
    {
        get
        {
            // Null check
            if (!TreeLayer.HasValue)
                return null;

            return (int)Mathf.Pow(2, TreeLayer.Value);
        }
    }
    public string TreeLayerName { get { return TreeLayer.HasValue ? LayerMask.LayerToName(TreeLayer.Value) : ""; } }
    public int? BranchLayer
    {
        get
        {
            // Null check
            if (branchSpawnerProperties.branchPrefab == null)
                return null;

            return branchSpawnerProperties.branchPrefab.layer;
        }
    }

    private LayerMask? BranchMask
    {
        get
        {
            // Null check
            if (!BranchLayer.HasValue)
                return null;

            return (int)Mathf.Pow(2, BranchLayer.Value);
        }
    }
    public string BranchLayerName { get { return BranchLayer.HasValue ? LayerMask.LayerToName(BranchLayer.Value) : "<null>"; } }

    public void Initialize(GameObject gameObj)
    {
        /* 
         * Easy initialize 
         */

        Go = gameObj;
        SpawnLocations = new List<BranchSpawnLocation>();
        MeshExt = new MeshExtended(Go);
        branchSpawnerProperties = new BranchSpawnerProperties();
        finalizationProperties = new MeshExtended.MeshFinalizationProperties();
    }

    [Serializable]
    public class BranchSpawnerProperties
    {
        /* 
         * The primary property class
         */

        public int numberSpawnPositions = 0;  // Number of spawn positions
        public AnimationCurve spawnDensity;  // Density of spawn points from trunk
        public bool castTopDown = false;  // When raycasting, only cast from top-down
        public float branchNeighborCollisionRadius = 0.1f, branchNeighborCollisionRadiusRaw = 0.1f;  // The radius of the collider to detect neighbor branches
        public int branchNeighborCollisionNumber = 0;  // The number of neighbors acceptable in the branches radius
        public GameObject branchPrefab; // Branch prefab
        public int branchOrientationMode = 0;  // Branch orientation mode
        public Vector3 branchMinScaleRaw = new Vector3(1, 1, 1), branchMaxScaleRaw = new Vector3(1, 1, 1);  // Spawn scale of branches, raw
        public Vector3 branchMinScale = new Vector3(1, 1, 1), branchMaxScale = new Vector3(1, 1, 1);  // Spawn scale of branches, adjusted for gameObject scale
        public Vector3 branchMinRotate, branchMaxRotate;  // Spawn rotation of branches
        public float heightBias;  // Bias of height from spawn location
        public AnimationCurve scaleCurve1, scaleCurve2, droopCurve, heightCurve, heightBiasCurve; // Several powers to adjust orientation, scale, etc.
        public float branchDroop = 0f;  // Droop of the branches
        public bool invertBranchDirection = true;  // Bool to invert branch direction

        public BranchSpawnerProperties()
        {
            // Initialize
            spawnDensity = AnimationCurve.Linear(0, 1, 1, 0);
            scaleCurve1 = AnimationCurve.EaseInOut(0, 1, 1, 0.3f);
            scaleCurve2 = AnimationCurve.EaseInOut(0, 1, 1, 0.4f);
            droopCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            droopCurve.AddKey(0.9f, 0.7f);
            heightCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
            heightBiasCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
            heightBiasCurve.AddKey(0.25f, 0.33f);
        }
    }

    public class BranchSpawnLocation
    {
        /* 
        * Class representing a spawn location, and some geometric 
        * properties of it
        */

        public Vector3 Location { get; set; }
        public float NormalizedBranchPosition { get; set; }  // Normalized 1..0 
        public float HeightFraction { get; set; }  // Normalized 0..1 
        public Vector3 SpawnDirection { get; set; }
        public Branch Branch { get; set; }
        public Branch Spawn(BranchSpawnerProperties bsp)
        {
            // Spawn the branch
            GameObject b = Instantiate(bsp.branchPrefab, Location, Quaternion.identity);

            // Note pass the height fraction and normalized position in as parameters so
            // the class has direct access to them
            Branch = new Branch(b, bsp, HeightFraction, NormalizedBranchPosition);
            return Branch;
        }
    }

    public class Branch
    {
        /* 
        * Class representing a branch object
        */

        private float m_heightFraction, m_normalizedBranchPosition;
        public Branch(GameObject gameobj, BranchSpawnerProperties bsp, float heightFraction, float normalizedBranchPosition)
        {
            // Initialize
            Go = gameobj;
            m_heightFraction = heightFraction;
            m_normalizedBranchPosition = normalizedBranchPosition;
            InitialPosition = Go.transform.position;
            InitialRotation = Go.transform.rotation;
            InitialScale = Go.transform.localScale;
            InitialScaleRandomizer = UnityEngine.Random.Range(bsp.branchMinScale.x, bsp.branchMaxScale.x);

            // Apply initial scale
            Scale(bsp);

            Go.transform.name = "branch";
        }
        public GameObject Go { get; }
        public Vector3 InitialPosition { get; }
        public Quaternion InitialRotation { get; set; }
        public Vector3 InitialRight { get { return InitialRotation * Vector3.right; } }
        public Vector3 InitialScale { get; }
        public float InitialScaleRandomizer { get; }
        public string Name { get; set; }

        public void Scale(BranchSpawnerProperties bsp)
        {
            // Scale diminishes based on height of the tree and position on branch
            float power1 = bsp.scaleCurve1.Evaluate(m_heightFraction);
            float power2 = bsp.scaleCurve2.Evaluate(m_normalizedBranchPosition);
            Go.transform.localScale = InitialScale * InitialScaleRandomizer * power1 * power2;
        }

        public void Droop(BranchSpawnerProperties bsp)
        {
            // For branches near the trunk of the tree, increase droop based on height of the tree
            if (m_normalizedBranchPosition == 0)
            {
                float power1 = bsp.droopCurve.Evaluate(m_heightFraction);
                Quaternion q = Quaternion.AngleAxis(-90 * power1, InitialRight) * InitialRotation;
                Go.transform.rotation = q;
            }
            else  // For branches not on the trunk, use dynamic droop
            {
                float power1 = bsp.branchDroop;
                Quaternion q = Quaternion.AngleAxis(-90 * power1, InitialRight) * InitialRotation;
                Go.transform.rotation = q;
            }
        }

        public void AdjustHeight(BranchSpawnerProperties bsp)
        {
            // For branches near the trunk of the tree, bias position upwards, to give a feathering effect
            float power1 = bsp.heightBiasCurve.Evaluate(m_normalizedBranchPosition);
            float heightBias = power1 * bsp.heightBias;
            Go.transform.position = InitialPosition + new Vector3(0, heightBias, 0);
        }
    }

    public void CreateColliders()
    {
        /* 
        * Create initial colliders
        */

        // Create the trunk collider, or replace it if it exists already
        CapsuleCollider cc = new CapsuleCollider();
        if (Go.TryGetComponent<CapsuleCollider>(out cc))
            DestroyImmediate(cc);

        // Add a new one
        Go.AddComponent(typeof(CapsuleCollider));

        // Create the mesh collider
        MeshCollider mc = new MeshCollider();
        if (!Go.TryGetComponent<MeshCollider>(out mc))
            Go.AddComponent(typeof(MeshCollider));
    }

    public void DetermineSpawnPositions(float radius)
    {
        /* 
        * Determines branch spawn positions by randomly
        * sphere casting within the mesh and checking for collision
        * radius = radius of the sphere cast (keep it small)
        */

        // Initial calcs
        CapsuleCollider trunk = Go.GetComponent<CapsuleCollider>();
        float treeHeight = MeshExt.Bounds.size.y, treeRadius = (MeshExt.Bounds.size.x / 2 + MeshExt.Bounds.size.z / 2) / 2;

        // Adjust for scale
        radius *= MeshExt.AverageScale;

        // Check if the tree is mostly just a trunk
        MostlyTrunk = (trunk.radius / treeRadius) > 0.6f;

        // Add all child game objects
        AddChildObjects();

        // Get the branch_spawns object
        GameObject child = Utilities.GetChild(Go, "branch_spawns");

        int hitCount = 0, maxIterations = 100000, j = 0;
        SpawnLocations = new List<BranchSpawnLocation>();
        while (hitCount < branchSpawnerProperties.numberSpawnPositions && j < maxIterations)
        {
            // Generate random spawn position within mesh bounds
            float xBias = MeshExt.Bounds.size.x / 2, zBias = MeshExt.Bounds.size.z / 2;
            if (MostlyTrunk)  // If just a trunk, allow the bias further outwards to provide more room to cast
            {
                xBias = MeshExt.Bounds.size.x;
                zBias = MeshExt.Bounds.size.z;
            }
            float randX = UnityEngine.Random.Range(Go.transform.position.x - xBias, Go.transform.position.x + xBias);
            float randY = UnityEngine.Random.Range(Go.transform.position.y, Go.transform.position.y + MeshExt.Bounds.size.y);
            float randZ = UnityEngine.Random.Range(Go.transform.position.z - zBias, Go.transform.position.z + zBias);
            Vector3 v = new Vector3(randX, randY, randZ);

            Vector3 direction = Vector3.down;
            if (!branchSpawnerProperties.castTopDown && !MostlyTrunk)
            {
                // If not top-down, generate random raycast direction
                direction = UnityEngine.Random.rotation * Vector3.one;
            } else if (MostlyTrunk)  // If just a trunk, just cast towards the trunk
            {
                direction = Go.transform.position - v;
                direction.y = 0;
            }

            // Test collision at position
            RaycastHit hit;
            if (Physics.SphereCast(v, radius, direction, out hit, 100, TreeMask.Value))
            {
                // Exclude any hits inside the trunk
                bool inTrunk = trunk.bounds.Contains(hit.point);
                // Search for branch neighbors
                Collider[] hitBranches = Physics.OverlapSphere(
                    hit.point,
                    branchSpawnerProperties.branchNeighborCollisionRadius,
                    BranchMask.Value);
                bool branchAllowed = (hitBranches.Length <= branchSpawnerProperties.branchNeighborCollisionNumber);

                if (!inTrunk && branchAllowed)
                {
                    // Calculate the chance of spawning in this position, based on density curve
                    // and normalized branch position (0 = trunk, 1 = branch end)
                    float normalizedPosition = 0f;  // Default to trunk
                    // Scan out the branch and determine the normalized position of the hit on the branch
                    if (!MostlyTrunk)
                    {
                        CalculateBranchDirection(
                            hit.point,
                            branchSpawnerProperties.branchNeighborCollisionRadius,
                            1000 * MeshExt.AverageScale,
                            TreeMask.Value,
                            out normalizedPosition, true);
                    }

                    // Get the chance to spawn in this location
                    float chance = branchSpawnerProperties.spawnDensity.Evaluate(normalizedPosition);

                    if (chance > UnityEngine.Random.Range(0f, 1f))
                    {
                        // Create the spawn location
                        BranchSpawnLocation spawn = new BranchSpawnLocation();
                        spawn.Location = hit.point;
                        spawn.NormalizedBranchPosition = normalizedPosition;
                        spawn.HeightFraction = (hit.point.y - Go.transform.position.y) / treeHeight;
                        spawn.SpawnDirection = direction;

                        // Add a collider for further branch detection
                        SphereCollider sc = child.AddComponent(typeof(SphereCollider)) as SphereCollider;
                        sc.center = hit.point;
                        sc.radius = branchSpawnerProperties.branchNeighborCollisionRadius;

                        // Add it to the collection
                        SpawnLocations.Add(spawn);

                        hitCount++;
                    }
                }
            }
            j++; // Keep track of max iterations
        }

        if (j >= maxIterations)
        {
            Debug.Log("Max iterations");
        }
    }

    public void AddChildObjects()
    {
        /* 
        * Add child gameobjects to hold all spawn objects
        */

        // Destroy existing objects
        Transform trans = Go.gameObject.transform.Find("branch_spawns");
        if (trans != null)
            DestroyImmediate(trans.gameObject);

        trans = Go.gameObject.transform.Find("branch_calculated_geometries");
        if (trans != null)
            DestroyImmediate(trans.gameObject);

        // Create new objects
        GameObject child = new GameObject();
        child.transform.name = "branch_spawns";
        child.transform.SetParent(Go.gameObject.transform);

        child = new GameObject();
        child.transform.name = "branch_calculated_geometries";
        child.transform.SetParent(Go.gameObject.transform);
    }

    private Vector3 CalculateBranchDirection(Vector3 fromPoint, float radius, float distance, LayerMask treeMask, out float normalizedPositionOnBranch, bool onlyOutwards = false)
    {
        /* 
        * The purpose of this routine is to iterate along a branch to determine its direction.
        * fromPoint = a random branch spawn location to start iterating from
        * radius = sphere cast radius for collision detection
        * distance = the overall distance to travel
        * treeMask = the tree mask for physics casts
        * normalizedPositionOnBranch = the normalized position of the location on the branch, based on it's overall length (0 = trunk, 1 = branch end)
        * onlyOutwards = whether or not to only traverse outwards, and not iterate backwards if starting at the end of a branch
        */

        Vector3 treeCenter = Go.transform.position;
        treeCenter.y = fromPoint.y;  // Match the y positions
        float distanceToTree = (fromPoint - treeCenter).magnitude;

        // If close enough to the trunk of the tree, just return the normal vector
        if (distanceToTree < (0.2 * MeshExt.AverageScale))
        {
            //Debug.Log("too close to iterate");
            normalizedPositionOnBranch = 0f;  // if it's this close, assume it's at trunk
            return Utilities.NormalDirection(treeCenter, fromPoint);
        }

        // Calculate iterations
        float distanceToCheck = radius * 2;  // 2 works well
        float f = distance / distanceToCheck;  // floating point errors prevent doing this directly
        int iterations = (int)f;
        iterations = Mathf.Clamp(iterations, 1, 100);  // constrain

        // Initialize the starting point of iterations, which is the
        // center of the tree, to the supplied sphere
        Vector3 from = treeCenter, to = fromPoint, next = Vector3.zero;

        int i = 0;
        bool converge = true, foundOne = false, backwards = false;
        while (i < iterations && converge)
        {
            converge = RecursiveBranchDetection(from, to, radius, distanceToCheck, treeMask, out next);
            if (converge)
            {
                // A "next" collision was found. Record it and iterate

                // Reset for the next iteration
                from = to;
                to = next;
                next = Vector3.zero;
                foundOne = true;
                i++;
            }
            // If it doesn't converge at all, inverse direction and try again
            // in the case of the end of a branch, for example
            else if (!converge && !foundOne && !backwards && !onlyOutwards)
            {
                //Debug.Log("failed to converge once, iterating backwards");
                converge = backwards = true;

                /* 
                * Note, the convergence routine normally starts looking for 
                * collisions one distance after the "to" parameter, which when reversed 
                * becomes the center of the tree (from). This doesn't work for this case.
                * Instead, multiply the backwards direction vector by a very short distance.
                */
                Vector3 newTo = to + Utilities.NormalDirection(to, from) * 0.01f;
                from = to;
                to = newTo;
            }
        }

        if (!foundOne)
        {
            // if iterations fail from the start, assume we're at the end of the branch
            normalizedPositionOnBranch = 1f;
        }
        else
        {
            // Calculate the normalized position (0 = trunk, 1 = branch end)
            normalizedPositionOnBranch = (distanceToTree / (distanceToTree + (to - fromPoint).magnitude));
        }

        if (backwards)
        {
            return fromPoint - to;
        }
        else
        {
            return to - fromPoint;
        }
    }

    private bool RecursiveBranchDetection(Vector3 from, Vector3 to, float radius, float distanceToCheck, LayerMask treeMask, out Vector3 next)
    {
        /* 
        * The purpose of this routine is to iterate in front of the "to" position, and locate the branch. 
        * from = the "from" point of the direction vector
        * to = to "to" point of the direction vector
        * radius = radius of the SphereCast
        * distanceToCheck = the distance in front of the "to" point to check for the branch
        * treeMask = the tree mask for physics casts
        * next = the average position of all hits
        */

        // Get the branch_calculated_geometries object
        GameObject child = Utilities.GetChild(Go, "branch_calculated_geometries");

        Vector3 normalDirection = Utilities.NormalDirection(from, to);
        next = Vector3.zero;

        bool hitTree = false;
        Vector3 testPoint = to + (normalDirection * distanceToCheck);  // point in front of the "to" point, adjusted by distanceToCheck
        Vector3 perpendicular = Utilities.QuatFromVector(normalDirection) * Vector3.up;  // perpendicular from test direction
        perpendicular *= 0.3f * MeshExt.AverageScale;  // adjust the perpendicular length by 0.3 (pretty good number)

        // Operate around the test point, and cast spheres towards it
        // Average all hits
        List<Vector3> hits = new List<Vector3>();
        RaycastHit hit;
        int iterations = 10;  // good number
        for (int i = 0; i < iterations; i++)
        {
            Quaternion q = Quaternion.AngleAxis((i * 360 / iterations), normalDirection);
            Vector3 castPoint = (testPoint + q * perpendicular);

            // For debugging casts
            //SphereCollider sc1 = child.AddComponent(typeof(SphereCollider)) as SphereCollider;
            //sc1.center = castPoint;
            //sc1.radius = radius;
            //m_debugVectors.Add(new DebugVector(castPoint, (testPoint - castPoint) * 0.2f));

            if (Physics.SphereCast(castPoint, radius, testPoint - castPoint, out hit, (testPoint - castPoint).magnitude, treeMask))
            {
                hits.Add(hit.point);
                // For debugging casts
                //SphereCollider sc1 = child.AddComponent(typeof(SphereCollider)) as SphereCollider;
                //sc1.center = hit.point;
                //sc1.radius = radius;
            }
        }

        // After all casts are done, average all vectors and return
        int len = hits.ToArray().Length;
        if (len > 0)
        {
            hitTree = true;
            foreach (Vector3 v in hits)
            {
                next += v;
            }
            next /= len;

            // For debugging casts
            //SphereCollider sc1 = child.AddComponent(typeof(SphereCollider)) as SphereCollider;
            //sc1.center = next;
            //sc1.radius = radius;
        }

        return hitTree;

    }

    public void SpawnBranches()
    {
        /* 
        * Void to spawn branches
        */

        // Add/remove the branches gameobject
        GameObject child = new GameObject();
        Transform branch_trans = Go.gameObject.transform.Find("branches");

        // Destroy existing objects
        if (branch_trans != null)
            DestroyImmediate(branch_trans.gameObject);

        child.transform.name = "branches";
        child.transform.position = Go.transform.position;
        child.transform.SetParent(Go.gameObject.transform);

        // Add branches
        foreach (BranchSpawnLocation bsl in SpawnLocations)
        {
            Vector3 v = bsl.Location;
            Quaternion q = Quaternion.identity;
            Branch branch = bsl.Spawn(branchSpawnerProperties);  // Spawn the branch

            /* 
            * Apply orientation
            * Note this isn't built into the BranchSpawnLocation or
            * Branch classes because the algorithm requires access to 
            * the parent game object available only in this parent class
            */
            Vector3 direction = Vector3.zero;  // Initialize
            if (branchSpawnerProperties.branchOrientationMode == 0)  // Full custom, use rotation sliders
            {
                q = Quaternion.Euler(
                    UnityEngine.Random.Range(branchSpawnerProperties.branchMinRotate.x, branchSpawnerProperties.branchMaxRotate.x),
                    UnityEngine.Random.Range(branchSpawnerProperties.branchMinRotate.y, branchSpawnerProperties.branchMaxRotate.y),
                    UnityEngine.Random.Range(branchSpawnerProperties.branchMinRotate.z, branchSpawnerProperties.branchMaxRotate.z)
                    );
            }
            else if (branchSpawnerProperties.branchOrientationMode == 1)  // Normal to trunk
            {
                Vector3 treeCenter = Go.transform.position;
                treeCenter.y = v.y;  // Match the y positions

                // If the tree is a trunk, use the spawn direction to spawn branches. It already is the normal.
                direction = (MostlyTrunk) ? bsl.SpawnDirection : Utilities.NormalDirection(treeCenter, v);
                q = Utilities.QuatFromVector(direction, branchSpawnerProperties.invertBranchDirection);
            }
            else if (branchSpawnerProperties.branchOrientationMode == 2)  // Conform to branch geometry
            {
                MeshExtended mesh = new MeshExtended(branch.Go);

                // The length of the branch is in the z-direction
                float z = mesh.Bounds.size.z;

                // Get conforming branch direction based on length 
                float normalizedPosition;
                direction = CalculateBranchDirection(
                    v,
                    branchSpawnerProperties.branchNeighborCollisionRadius,
                    z,
                    TreeMask.Value,
                    out normalizedPosition);

                // Get the quaternion from the vector
                q = Utilities.QuatFromVector(direction, branchSpawnerProperties.invertBranchDirection);

            }

            // Reassign the initial rotation of the branch
            branch.Go.transform.rotation = branch.InitialRotation = q;

            // Add the branch to the parent
            branch.Go.transform.SetParent(child.transform);
        }
    }

    public void AffectBranches()
    {
        /* 
        * Void to affect branches
        */

        foreach (BranchSpawnLocation b in SpawnLocations)
        {
            // Apply any new parameters
            b.Branch.Scale(branchSpawnerProperties);
            b.Branch.Droop(branchSpawnerProperties);
            b.Branch.AdjustHeight(branchSpawnerProperties);
        }

        // Update last parameters
        LastDroop = branchSpawnerProperties.branchDroop;
        LastScaleCurve1 = branchSpawnerProperties.scaleCurve1;
        LastScaleCurve2 = branchSpawnerProperties.scaleCurve2;
        LastDroopCurve = branchSpawnerProperties.droopCurve;
        LastHeightCurve = branchSpawnerProperties.heightCurve;
        LastHeightBiasCurve = branchSpawnerProperties.heightBiasCurve;
    }

    // Maintain "Last" parameters to known when to modify the interface
    public float LastDroop { get; set; }
    public AnimationCurve LastScaleCurve1 { get; set; }
    public AnimationCurve LastScaleCurve2 { get; set; }
    public AnimationCurve LastDroopCurve { get; set; }
    public AnimationCurve LastHeightCurve { get; set; }
    public AnimationCurve LastHeightBiasCurve { get; set; }   

    public GameObject Finalize()
    {
        /* 
        * Function for finalizing the tree
        */

        GameObject LOD = MeshExt.FinalizeMesh(finalizationProperties);

        // Add the trunk collider
        CapsuleCollider cc = gameObject.GetComponent<CapsuleCollider>(), cc1 = LOD.AddComponent<CapsuleCollider>();
        cc1.center = cc.center;
        cc1.radius = cc.radius;
        cc1.height = cc.height;
        cc1.direction = cc.direction;
        cc1.transform.position = cc.transform.position;

        return LOD;
    }
}

