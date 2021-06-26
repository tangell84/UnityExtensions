/* **********************************
 * Tom Tools, designed for Unity 2019.3
 * - A collection of general helper functions
 *   
 * Author: Tom Angell, May 2020
 * All Rights Reserved
 * **********************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

namespace Tom.Traditional
{
    public static class Utilities
    {
        public static Vector3 NormalDirection(Vector3 from, Vector3 to)
        {
            /* 
             * Returns normal direction of to-from
             */

            Vector3 pathVector = to - from;
            float distance = pathVector.magnitude;
            Vector3 normal = pathVector / distance;

            return normal;

        }

        public static Quaternion QuatFromVector (Vector3 dir, bool inverse = false)
        {
            /* 
             * Returns a quaternion in the direction of vector, with normal up
             */

            //float hyp = v.magnitude;
            //float opp = v.y;
            //float xy = Mathf.Asin(opp / hyp) * 360 / (2 * Mathf.PI);
            //Debug.Log("xy angle  is: " + xy + " degrees");

            Vector3 forward = new Vector3(dir.x, 0, dir.z);
            Vector3 right = Quaternion.Euler(0, 90f, 0) * forward;
            //Vector3 up = Quaternion.AngleAxis(xy, right) * forward;
            Vector3 up = Quaternion.AngleAxis(90f, dir) * right;

            if (inverse) { dir *= -1; }

            return Quaternion.LookRotation(dir, up);
        }

        public static GameObject GetChild(GameObject go, string name)
        {
            /* 
             * Gets a child by name
             */

            Transform trans = go.gameObject.transform.Find(name);
            if (trans == null)
            {
                return null;
            } else
            {
                return trans.gameObject;
            }
        }

        public static void DestroyObjectByName(string name)
        {
            /* 
             * Destroys a gameobject by name
             */

            GameObject go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        public static void DestroyChildren (GameObject go)
        {
            /* 
             * Destroys all children objects
             */

            int count = go.transform.childCount;  // store count initially, because value changes in the for-loop
            for (int i = 0; i < count; i++)
            {
                // Always destroy index 0, as the count changes as children are destroyed
                Object.DestroyImmediate(go.transform.GetChild(0).gameObject);
            }
        }

        public static Material CreateURPMaterial(Texture mainTexture, Color color, bool cutOut, bool transparent, float renderFace)
        {
            /* 
             * Builds a URP material dynamically
             * https://docs.unity3d.com/Manual/MaterialsAccessingViaScript.html
             * Also, load the URP shader editor and inspect other properties there
             */

            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            //Material material = new Material(Shader.Find("Specular"));

            if (cutOut)  // Cut-out mode
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_AlphaClip", 1);
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetFloat("_AlphaClip", 0);
            }

            if (transparent)  // Transparent mode
            {
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetFloat("_Surface", 1);
            }
            else
            {
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetFloat("_Surface", 0);
            }

            // 0 = Both, 1 = Back, 2 = Front
            material.SetFloat("_Cull", renderFace);
            material.SetFloat("_AlphaClip", 1);
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", mainTexture);

            return material;
        }

        public static Dictionary<string, object> BuildDictionary(List<string> keys, List<string> values)
        {
            /*
             * Builds a dictionary using the input parameters
             * Note: The inputs are both string lists, which are usable as public parameters in Unity
             * (as opposed to List<object>)
             */

            // Validate
            if (keys.ToArray().Length != values.ToArray().Length)
            {
                Debug.LogError("Cannot create dictionary; key and value length do not match.");
                return null;
            }

            // Build the dictionary
            Dictionary<string, object> dict = new Dictionary<string, object>();
            for (int i = 0; i < keys.ToArray().Length; i++)
            {
                dict.Add(keys[i], values[i]);
            }

            return dict;
        }

        public static Vector3 VectorToMouse(Vector3 worldPoint, Camera camera, bool invertX = false, bool invertY = false)
        {
            /* 
             * Returns a vector from the specified world point to
             * the mouse position
             * TCA 4/2/21
             */

            // Inverting isn't simply -1, it depends on screen pixels

            // Check for invert X
            var mouseX = Input.mousePosition.x;
            if (invertX)
            {
                var centerX = camera.pixelWidth / 2;
                var distanceToCenterX = mouseX - centerX;
                mouseX = centerX - distanceToCenterX;
            }

            // Check for invert Y
            var mouseY = Input.mousePosition.y;
            if (invertY)
            {
                var centerY = camera.pixelHeight / 2;
                var distanceToCenterY = mouseY - centerY;
                mouseY = centerY - distanceToCenterY;
            }

            // Build vector
            var mousePoint = new Vector3(mouseX, mouseY, camera.nearClipPlane);
            return camera.ScreenToWorldPoint(mousePoint) - worldPoint;
        }

        public static Vector3 MouseVectorFromCenter(Camera camera, bool invertX = false, bool invertY = false)
        {
            /* 
             * Returns a vector from the center of the camera to
             * the mouse position
             * TCA 4/2/21
             */

            var centerX = camera.pixelWidth / 2;
            var centerY = camera.pixelHeight / 2;

            // Inverting isn't simply -1, it depends on screen pixels

            // Check for invert X
            var mouseX = Input.mousePosition.x;
            if (invertX)
            {
                var distanceToCenterX = mouseX - centerX;
                mouseX = centerX - distanceToCenterX;
            }

            // Check for invert Y
            var mouseY = Input.mousePosition.y;
            if (invertY)
            {
                var distanceToCenterY = mouseY - centerY;
                mouseY = centerY - distanceToCenterY;
            }

            // Build vector
            var mouseVector = new Vector3(mouseX - centerX, mouseY - centerY, 0f);
            return mouseVector;
        }

        public static bool ApplicationHasFocus()
        {
            /* 
             * Returns whether or not the application (including editor)
             * has focus
             * TCA 4/2/21
             */

            var hasFocus = false;
#if UNITY_EDITOR
            hasFocus = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
#else
            hasFocus = Application.isFocused;
#endif
            return hasFocus;
        }

        public static bool ReadBit(int word, int bitPosition)
        {
            /* 
             * Reads the bit in the specified bit position
             * TCA 4/7/21
             */

            return (word & (1 << bitPosition)) != 0;
        }

        public static int SetBit(int word, int bitPosition)
        {
            /* 
             * Sets the bit in the specified bit position
             * and returns the modified word
             * TCA 4/7/21
             */

            return word |= 1 << bitPosition;
        }

        public static int ClearBit(int word, int bitPosition)
        {
            /* 
             * Clears the bit in the specified bit position
             * and returns the modified word
             * TCA 4/7/21
             */

            return word &= ~(1 << bitPosition);
        }

        public static int ToggleBit(int word, int bitPosition)
        {
            /* 
             * Toggles the bit in the specified bit position
             * and returns the modified word
             * TCA 4/7/21
             */

            return word ^= 1 << bitPosition;
        }
    }

    public static class TomLOD
    {
        public static LOD BuildLOD(string name, Vector3 pos, Mesh mesh, Material[] mat, float transition, out GameObject lodGo)
        {
            /*
             * Builds a LOD
             */

            // Build the game object
            lodGo = new GameObject(name);
            lodGo.transform.position = pos;
            MeshFilter mf = lodGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = lodGo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mat;

            // Get the renderer
            Renderer[] renderer = lodGo.GetComponents<Renderer>();

            // Build the LOD
            LOD lod = new LOD(transition, renderer);

            return lod;
        }
    }

    public static class TomUI
    {
        public static GameObject ToggleUIOverlay (GameObject prefab, Transform parent = null)
        {
            /*
             * Toggles (spawns or destroys) the prefab with "UIOverlay"
             * Returns reference to the GameObject if created
             */

            GameObject[] overlays = GameObject.FindGameObjectsWithTag("UIOverlay");
            if (overlays.Length == 0)
            {
                if (parent == null)
                {
                    return Object.Instantiate(prefab);
                } else
                {
                    return Object.Instantiate(prefab, parent);
                }
            }
            else
            {
                foreach (var go in overlays)
                {
                    Object.Destroy(go);
                }
                return null;
            }
        }
    }
}

namespace Tom.REST
{
    public class REST
    {
        /*
         * A collection of REST helper functions
         */

        public class Post
        {
            /*
             * A class to hold the result of a REST function,
             * in this case POST
             * From: https://answers.unity.com/questions/24640/how-do-i-return-a-value-from-a-coroutine.html
             */
            public object result { get; private set; }
            public float executionTime { get; private set; }
            public int size { get; private set; }
            public long responseCode { get; private set; }
            public Coroutine coroutine { get; private set; }
            private float m_startTime;

            public Post(MonoBehaviour mono, string uri, string json)
            {
                coroutine = mono.StartCoroutine(RESTPost(uri, json));
            }

            private IEnumerator RESTPost(string uri, string json)
            {
                m_startTime = Time.time;
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                Debug.Log("posted " + bytes.Length + " bytes");
                UnityWebRequest post = new UnityWebRequest(uri, "POST");
                post.uploadHandler = new UploadHandlerRaw(bytes);
                post.downloadHandler = new DownloadHandlerBuffer();
                post.SetRequestHeader("Content-Type", "application/json");
                yield return post.SendWebRequest();

                if (post.isNetworkError || post.isHttpError)
                {
                    Debug.LogError(string.Format("Error with POST request - {0}", post.error));
                    yield break;
                }
                executionTime = Time.time - m_startTime;
                result = post.downloadHandler.text;
                size = post.downloadHandler.data.Length;
                Debug.Log("returned " + size + " bytes");
                responseCode = post.responseCode;
                yield return result;
            }
        }
    }
}

namespace Tom.SystemIO
{
    public static class Utilities
    {
        public static string[] DecodeFolderPath(string path, int appendSettings = 0)
        {
            /*
             * Function to decode the supplied path into folder and object,
             * and provide validation checking.
             * appendSettings = 0, appends nothing
             * appendSettings = 1, appends Assets/
             * appendSettings = 2, appends FQ path
             * return[0] is the folder
             * return[1] is the object
             */

            string folder, objectName;

            // First, strip off anything up to and including "Assets", if supplied
            if (path.Contains("Assets/"))
            {
                path = path.Substring(path.IndexOf("Assets/") + 7);
            }

            // Next strip off any leading /
            path = path.TrimStart(new char[] { '/' });

            // Next, separate the path and object name, based on trailing /
            if (path.EndsWith("/"))
            {
                path = path.TrimEnd(new char[] { '/' });  // Trim trailing /
                folder = path;
                objectName = "defaultObject"; // if no object name provided, default it
            }
            else
            {
                folder = path.Substring(0, path.LastIndexOf("/"));
                objectName = path.Substring(path.LastIndexOf("/") + 1);
            }

            // Finally, append different folders, if requested
            folder = (appendSettings == 1) ? "Assets/" + folder : folder;
            folder = (appendSettings == 2) ? Application.dataPath + "/" + folder : folder;

            string[] result = new string[2];
            result[0] = folder;
            result[1] = objectName;

            return result;
        }
    }
}