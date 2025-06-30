using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using Unity.HLODSystem.Utils;

namespace Unity.HLODSystem{
    public class ManualSimplificationBatcher : IBatcher{
        private SerializableDynamicObject m_batcherOptions;

        public ManualSimplificationBatcher(SerializableDynamicObject batcherOptions){
            m_batcherOptions = batcherOptions;
        }

        [InitializeOnLoadMethod]
        static void RegisterType(){
            BatcherTypes.RegisterBatcherType(typeof(ManualSimplificationBatcher));
        }

        public void Dispose() { }

        public void Batch(Transform rootTransform, DisposableList<HLODBuildInfo> targets, bool castShadows, Action<float> onProgress){
            dynamic options = m_batcherOptions;
            string exportPath = options.ExportPath ?? "Assets/HLOD_Exports/";
            bool skipAutoSimplification = options.SkipAutoSimplification ?? true;

            if (!Directory.Exists(exportPath))
                Directory.CreateDirectory(exportPath);

            for (int i = 0; i < targets.Count; ++i){
                var info = targets[i];

                // Объединяем меши
                WorkingMesh combinedMesh = CombineMeshes(rootTransform, info);

                // Экспортируем в FBX
                string fbxPath = ExportToFBX(combinedMesh, info.Name, exportPath);

                // Если включен режим ручного упрощения
                if (skipAutoSimplification){
                    // Создаем заглушку для системы
                    CreatePlaceholderObject(info, fbxPath);
                }else{
                    // Используем объединенный меш как есть
                    ApplyCombinedMesh(info, combinedMesh, castShadows);
                }
                onProgress?.Invoke((float)i / targets.Count);
            }
            AssetDatabase.Refresh();
        }

        private WorkingMesh CombineMeshes(Transform rootTransform, HLODBuildInfo info){
            MeshCombiner combiner = new MeshCombiner();
            List<MeshCombiner.CombineInfo> combineInfos = new List<MeshCombiner.CombineInfo>();

            Matrix4x4 hlodWorldToLocal = rootTransform.worldToLocalMatrix;

            for (int i = 0; i < info.WorkingObjects.Count; ++i){
                var obj = info.WorkingObjects[i];
                Matrix4x4 matrix = hlodWorldToLocal * obj.LocalToWorld;

                for (int m = 0; m < obj.Mesh.subMeshCount; ++m){
                    combineInfos.Add(new MeshCombiner.CombineInfo{
                        Transform = matrix,
                        Mesh = obj.Mesh,
                        MeshIndex = m
                    });
                }
            }

            return combiner.CombineMesh(Allocator.Persistent, combineInfos);
        }

        #if UNITY_FBXEXPORTER
        private string ExportToFBX(WorkingMesh mesh, string name, string path){
            GameObject tempGO = new GameObject(name);
            MeshFilter mf = tempGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh.ToMesh();

            string fbxPath = path + name + ".fbx";
            ModelExporter.ExportObject(fbxPath, tempGO);

            GameObject.DestroyImmediate(tempGO);
            return fbxPath;
        }
        #endif

        private void CreatePlaceholderObject(HLODBuildInfo info, string fbxPath){
            // Создаем метаданные для последующей загрузки
            dynamic metadata = new SerializableDynamicObject();
            metadata.OriginalPath = fbxPath;
            metadata.NeedsManualSimplification = true;

            // Сохраняем метаданные в info для дальнейшей обработки
            info.WorkingObjects[0].Name = info.Name + "_NEEDS_SIMPLIFICATION";
        }

        private void ApplyCombinedMesh(HLODBuildInfo info, WorkingMesh mesh, bool castShadows){
            WorkingObject newObj = new WorkingObject(Allocator.Persistent);
            newObj.Name = info.Name;
            newObj.SetMesh(mesh);
            newObj.CastShadow = castShadows;

            // Копируем материалы
            if (info.WorkingObjects.Count > 0){
                newObj.Materials.AddRange(info.WorkingObjects[0].Materials);
            }

            info.WorkingObjects.Clear();
            info.WorkingObjects.Add(newObj);
        }

        public static void OnGUI(HLOD hlod, bool isFirst){
            EditorGUI.indentLevel += 1;
            dynamic options = hlod.BatcherOptions;

            if (options.ExportPath == null)
                options.ExportPath = "Assets/HLOD_Exports/";
            if (options.SkipAutoSimplification == null)
                options.SkipAutoSimplification = true;

            EditorGUILayout.LabelField("Manual Simplification Settings");

            options.ExportPath = EditorGUILayout.TextField("Export Path", options.ExportPath);
            options.SkipAutoSimplification = EditorGUILayout.Toggle("Skip Auto Simplification",
                options.SkipAutoSimplification);

            if (options.SkipAutoSimplification){
                EditorGUILayout.HelpBox(
                    "Meshes will be exported for manual simplification. " +
                    "Use external tools to simplify, then reimport.",
                    MessageType.Info);
            }

            EditorGUI.indentLevel -= 1;
        }
    }
}
