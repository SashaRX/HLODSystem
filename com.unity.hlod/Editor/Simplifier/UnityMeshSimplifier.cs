using System;
using System.Collections;
using Unity.Collections;
using Unity.HLODSystem.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.HLODSystem.Simplifier{
    public class UnityMeshSimplifier(SerializableDynamicObject simplifierOptions) : SimplifierBase(simplifierOptions), SimplifierBase{
        [InitializeOnLoadMethod]
        static void RegisterType(){
            SimplifierTypes.RegisterType(typeof(UnityMeshSimplifier));
        }

        protected override IEnumerator GetSimplifiedMesh(Utils.WorkingMesh origin, float quality, Action<Utils.WorkingMesh> resultCallback){
            var meshSimplifier = new global::UnityMeshSimplifier.MeshSimplifier{
                Vertices = origin.vertices,
                Normals = origin.normals,
                Tangents = origin.tangents,
                UV1 = origin.uv,
                UV2 = origin.uv2,
                UV3 = origin.uv3,
                UV4 = origin.uv4,
                Colors = origin.colors
            };

            var triangles = new int[origin.subMeshCount][];
            for (var submesh = 0; submesh < origin.subMeshCount; submesh++){
                triangles[submesh] = origin.GetTriangles(submesh);
            }

            meshSimplifier.AddSubMeshTriangles(triangles);

            meshSimplifier.SimplifyMesh(quality);

            int triCount = 0;
            for (int i = 0; i < meshSimplifier.SubMeshCount; ++i){
                triCount += meshSimplifier.GetSubMeshTriangles(i).Length;
            }

            Utils.WorkingMesh nwm = new WorkingMesh(Allocator.Persistent, meshSimplifier.Vertices.Length, triCount, meshSimplifier.SubMeshCount, 0);
            nwm.name = origin.name;
            nwm.vertices = meshSimplifier.Vertices;
            nwm.normals = meshSimplifier.Normals;
            nwm.tangents = meshSimplifier.Tangents;
            nwm.uv = meshSimplifier.UV1;
            nwm.uv2 = meshSimplifier.UV2;
            nwm.uv3 = meshSimplifier.UV3;
            nwm.uv4 = meshSimplifier.UV4;
            nwm.colors = meshSimplifier.Colors;
            nwm.subMeshCount = meshSimplifier.SubMeshCount;
            for (var submesh = 0; submesh < nwm.subMeshCount; submesh++){
                nwm.SetTriangles(meshSimplifier.GetSubMeshTriangles(submesh), submesh);
            }

            resultCallback?.Invoke(nwm);
            yield break;
        }

        public static void OnGUI(SerializableDynamicObject simplifierOptions){
            OnGUIBase(simplifierOptions);
        }
    }
}
