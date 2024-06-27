using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;


namespace ASP
{
    [ExecuteInEditMode]
    public class ASPCharacterPanel : MonoBehaviour
    {
        public enum OverrideMode
        {
            NO_OVERRIDE,
            OVERRIDE_WITH_EULER,
            //USE_SCENELUIGHT_HALFVECTOR,
        }

        public enum BackFaceOutlineMethod
        {
            FROM_VERTEX_NORMAL,
            FROM_UV4
        }

        [System.Serializable]
        public class MaterialGroup
        {
            public Material[] Materials;
        }

        public BackFaceOutlineMethod CurrentBackFaceOutlineMethod;
        public Color BackfaceOutlineColor;
        public float BackFaceOutlineWidth;

        public Vector2 BackFaceOutlineFadeStartEnd = new Vector2(50, 50);
        public float ScaleWidthAsScreenSpaceOutline = 0f;
        public Vector3 CenterPositionOffset = Vector3.zero;
        public OverrideMode m_overrideMode;
        public Vector3 m_overrideLightAngle;
        public Transform HeadBoneTransform;
        public Vector3 CharacterCenterWS
        {
            get { return transform.position + CenterPositionOffset; }
        }

        public void ForGizmo(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f,
            float arrowHeadAngle = 20.0f)
        {
            Gizmos.DrawRay(pos, direction);

            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) *
                            new Vector3(0, 0, 1);
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) *
                           new Vector3(0, 0, 1);
            Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
            Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
        }

        public void ForGizmo(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f,
            float arrowHeadAngle = 20.0f)
        {
            Gizmos.color = color;
            Gizmos.DrawRay(pos, direction);

            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) *
                            new Vector3(0, 0, 1);
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) *
                           new Vector3(0, 0, 1);
            Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
            Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
        }


        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;

            ForGizmo(transform.position, transform.forward, 0.35f, 20f);
        }

        public void CreateMaterialInstance()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var matList = new List<Material>();
                foreach (var mat in renderer.sharedMaterials)
                {
                    if(mat == null || mat.shader == null)
                        continue;
                    var newMat = new Material(mat);
                    newMat.name = mat.name + "_Instance";
                    matList.Add(newMat);
                }
            
                renderer.materials = matList.ToArray();
            }
        }

        private static void SetKeyword(Material material, string keyword, bool state)
        {
            //UnityEngine.Debug.Log(keyword + " = "+state);
            if (state)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        public bool HasUnmatchedCharacterCenter()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if(mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")))
                    {
                        continue;
                    }

                    if (mat.HasVector("_CharacterCenterWS") &&
                        Vector3.Distance(mat.GetVector("_CharacterCenterWS"), CharacterCenterWS) > 0.5f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        
        
        
        public void SetupMaterialID()
        {
            var materialIndex = 1;
            var materialNames = new List<string>();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if(mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")))
                    {
                        continue;
                    }

                    materialIndex += 1;
                    if (!materialNames.Contains(mat.name))
                    {
                        if (mat.GetFloat("_MaterialID") <= 0)
                        {
                            mat.SetFloat("_MaterialID", materialIndex++);
                        }
                    }
                }
            }
        }
        
        public void UpdateLightDirectionOverrideParam()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var subMaterialIndex = 0;
                Material[] mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if(mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")))
                    {
                        continue;
                    }
                    
                    mat.SetVector("_FaceFrontDirection", HeadBoneTransform != null ? HeadBoneTransform.forward : transform.forward);
                    mat.SetVector("_FaceRightDirection", HeadBoneTransform != null ? HeadBoneTransform.right : transform.right);
                    mat.SetVector("_CharacterCenterWS", CharacterCenterWS);
                    mat.SetFloat("_OverrideLightDirToggle", m_overrideMode != OverrideMode.NO_OVERRIDE ? 1 : 0);
                    mat.SetVector("_FakeLightEuler",
                        new Vector3(180 + m_overrideLightAngle.x, m_overrideLightAngle.y, m_overrideLightAngle.z));
                    subMaterialIndex++;
                }
            }
        }

        private void TryCreateDummyShadowCaster()
        {
            var hasDummy = false;
            foreach (var obj in Camera.main.gameObject.GetComponentsInChildren<Renderer>())
            {
                if (obj.gameObject.name == "ASPDummyShadowCaster(Don't Delete This)")
                {
                    hasDummy = true;
                    break;
                }
            }

            if (!hasDummy)
            {
                var dummyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummyObj.name = "ASPDummyShadowCaster(Don't Delete This)";
                dummyObj.hideFlags = HideFlags.NotEditable;
                dummyObj.hideFlags = HideFlags.DontSave;
                dummyObj.transform.SetParent(Camera.main.transform);
                dummyObj.transform.localPosition = new Vector3(0,0,1);
                dummyObj.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                dummyObj.GetComponent<Renderer>().material = new Material(Shader.Find("Hidden/ASP/DummyShadowCaster"));
                if( dummyObj.GetComponent<Collider>())
                    DestroyImmediate(dummyObj.GetComponent<Collider>());
            }
        }
        
        public void SetDebugGIFlagToAllMaterials(float value)
        {
            
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var subMaterialIndex = 0;
                Material[] mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character")))
                    {
                        continue;
                    }
                    mat.SetFloat("_DebugGI", Mathf.Clamp01(value));
                    subMaterialIndex++;
                }
            }
        }

        public void SetDitheringValueToAllMaterials(float value)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")))
                    {
                        continue;
                    }
                    mat.SetFloat("_Dithering", Mathf.Clamp01(value));
                }
            }
        }
        
        public void SetDitheringSizeValueToAllMaterials(float value)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")))
                    {
                        continue;
                    }
                    mat.SetFloat("_DitherTexelSize", value);
                }
            }
        }
        
        public void SetFOVAdjustValueToAllMaterials(float value)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")))
                    {
                        continue;
                    }
                    mat.SetFloat("_FOVShiftX", Mathf.Clamp01(value));
                }
            }
        }

        public bool CreateMaterialInstanceOnPlay;

        private void Start()
        {
            SetupMaterialID();
            //CreateMaterialInstance();
            // TryCreateDummyShadowCaster();
        }

        // Update is called once per frame
        void Update()
        {
#if UNITY_EDITOR
          //  TryCreateDummyShadowCaster();
#endif
            UpdateLightDirectionOverrideParam();
        }
    }
}