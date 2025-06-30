using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using UnityEditor.Formats.Fbx.Exporter;
using Unity.HLODSystem.Simplifier;
using Unity.HLODSystem.Utils;

namespace Unity.HLODSystem {
    public class ManualSimplificationBatcher : IBatcher {
        private SerializableDynamicObject m_batcherOptions;

        public ManualSimplificationBatcher(SerializableDynamicObject batcherOptions) {
            m_batcherOptions = batcherOptions;
        }

        [InitializeOnLoadMethod]
        static void RegisterType() {
            BatcherTypes.RegisterBatcherType(typeof(ManualSimplificationBatcher));
        }

        public void Dispose() { }

        public void Batch(Transform rootTransform, DisposableList<HLODBuildInfo> targets,
            bool castShadows, Action<float> onProgress) {
            dynamic options = m_batcherOptions;
            string exportPath = options.ExportPath ?? "Assets/HLOD_Exports/";
            bool exportAsFBX = options.ExportAsFBX ?? true;
            bool reimportProcessed = options.ReimportProcessed ?? false;

            if (!Directory.Exists(exportPath)) {
                Directory.CreateDirectory(exportPath);
            }

            for (int i = 0; i < targets.Count; ++i) {
                var info = targets[i];

                // Шаг 1: Объединяем меши
                WorkingMesh combinedMesh = CombineMeshes(rootTransform, info);

                // Шаг 2: Экспортируем
                string exportedPath = exportAsFBX ?
                    ExportToFBX(combinedMesh, info, exportPath) :
                    ExportAsAsset(combinedMesh, info.Name, exportPath);

                // Шаг 3: Проверяем наличие обработанного файла
                string processedPath = GetProcessedFilePath(exportedPath);
                if (reimportProcessed && File.Exists(processedPath)) {
                    ApplyProcessedMesh(info, processedPath, castShadows);
                } else {
                    ApplyCombinedMesh(info, combinedMesh, castShadows);
                }

                onProgress?.Invoke((float)i / targets.Count);
            }

            AssetDatabase.Refresh();
        }

        private WorkingMesh CombineMeshes(Transform rootTransform, HLODBuildInfo info) {
            MeshCombiner combiner = new MeshCombiner();
            List<MeshCombiner.CombineInfo> combineInfos = new List<MeshCombiner.CombineInfo>();

            Matrix4x4 hlodWorldToLocal = rootTransform.worldToLocalMatrix;

            for (int i = 0; i < info.WorkingObjects.Count; ++i) {
                var obj = info.WorkingObjects[i];
                Matrix4x4 matrix = hlodWorldToLocal * obj.LocalToWorld;

                for (int m = 0; m < obj.Mesh.subMeshCount; ++m) {
                    combineInfos.Add(new MeshCombiner.CombineInfo {
                        Transform = matrix,
                        Mesh = obj.Mesh,
                        MeshIndex = m
                    });
                }
            }

            return combiner.CombineMesh(Allocator.Persistent, combineInfos);
        }

        private string ExportToFBX(WorkingMesh mesh, HLODBuildInfo info, string path) {
            // Создаем временный GameObject для экспорта
            GameObject tempRoot = new GameObject(info.Name + "_HLOD");
            GameObject meshObject = new GameObject("CombinedMesh");
            meshObject.transform.SetParent(tempRoot.transform);

            MeshFilter mf = meshObject.AddComponent<MeshFilter>();
            MeshRenderer mr = meshObject.AddComponent<MeshRenderer>();

            // Конвертируем меш
            Mesh unityMesh = mesh.ToMesh();
            unityMesh.name = info.Name + "_Combined";
            mf.sharedMesh = unityMesh;

            // Применяем материалы
            List<Material> materials = new List<Material>();
            if (info.WorkingObjects.Count > 0) {
                foreach (var mat in info.WorkingObjects[0].Materials) {
                    materials.Add(mat.ToMaterial());
                }
            }
            mr.sharedMaterials = materials.ToArray();

            // Экспортируем в FBX
            string fbxPath = Path.Combine(path, info.Name + "_Combined.fbx");
            ModelExporter.ExportObject(fbxPath, tempRoot);

            // Очищаем временные объекты
            GameObject.DestroyImmediate(tempRoot);

            AssetDatabase.Refresh();
            return fbxPath;
        }

        private string ExportAsAsset(WorkingMesh mesh, string name, string path) {
            Mesh unityMesh = mesh.ToMesh();
            unityMesh.name = name + "_Combined";

            string assetPath = Path.Combine(path, name + "_Combined.asset");
            AssetDatabase.CreateAsset(unityMesh, assetPath);
            AssetDatabase.SaveAssets();

            return assetPath;
        }

        private string GetProcessedFilePath(string originalPath) {
            string directory = Path.GetDirectoryName(originalPath);
            string filename = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            return Path.Combine(directory, filename + "_Processed" + extension);
        }

        private void ApplyProcessedMesh(HLODBuildInfo info, string processedPath, bool castShadows) {
            GameObject processedObject = AssetDatabase.LoadAssetAtPath<GameObject>(processedPath);
            if (processedObject == null) {
                Debug.LogError($"Failed to load processed mesh: {processedPath}");
                return;
            }

            MeshFilter mf = processedObject.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) {
                Debug.LogError($"No valid mesh found in: {processedPath}");
                return;
            }

            // Создаем новый WorkingMesh из обработанного
            WorkingMesh workingMesh = mf.sharedMesh.ToWorkingMesh(Allocator.Persistent);

            WorkingObject newObj = new WorkingObject(Allocator.Persistent);
            newObj.Name = info.Name;
            newObj.SetMesh(workingMesh);
            newObj.CastShadow = castShadows;

            // Копируем материалы
            CopyMaterials(info, newObj);

            info.WorkingObjects.Clear();
            info.WorkingObjects.Add(newObj);
        }

        private void ApplyCombinedMesh(HLODBuildInfo info, WorkingMesh mesh, bool castShadows) {
            WorkingObject newObj = new WorkingObject(Allocator.Persistent);
            newObj.Name = info.Name;
            newObj.SetMesh(mesh);
            newObj.CastShadow = castShadows;

            CopyMaterials(info, newObj);

            info.WorkingObjects.Clear();
            info.WorkingObjects.Add(newObj);
        }

        private void CopyMaterials(HLODBuildInfo info, WorkingObject targetObj) {
            if (info.WorkingObjects.Count > 0 && info.WorkingObjects[0].Materials.Count > 0) {
                foreach (var mat in info.WorkingObjects[0].Materials) {
                    targetObj.Materials.Add(mat.Clone());
                }
            } else {
                Material defaultMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                WorkingMaterial workingMat = defaultMat.ToWorkingMaterial(Allocator.Persistent);
                targetObj.Materials.Add(workingMat);
            }
        }

        public static void OnGUI(HLOD hlod, bool isFirst) {
            EditorGUI.indentLevel += 1;
            dynamic options = hlod.BatcherOptions;

            if (options.ExportPath == null) {
                options.ExportPath = "Assets/HLOD_Exports/";
            }
            if (options.ExportAsFBX == null) {
                options.ExportAsFBX = true;
            }
            if (options.ReimportProcessed == null) {
                options.ReimportProcessed = false;
            }

            EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);

            options.ExportPath = EditorGUILayout.TextField("Export Path", options.ExportPath);
            options.ExportAsFBX = EditorGUILayout.Toggle("Export as FBX", options.ExportAsFBX);
            options.ReimportProcessed = EditorGUILayout.Toggle("Auto-import Processed", options.ReimportProcessed);

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Workflow:\n" +
                "1. Generate - exports combined meshes\n" +
                "2. Edit exported files in 3D software\n" +
                "3. Save as '_Processed' suffix\n" +
                "4. Generate again to apply changes",
                MessageType.Info);

            if (GUILayout.Button("Open Export Folder")) {
                string fullPath = Path.GetFullPath(options.ExportPath);
                if (!Directory.Exists(fullPath)) {
                    Directory.CreateDirectory(fullPath);
                }
                EditorUtility.RevealInFinder(fullPath);
            }

            EditorGUI.indentLevel -= 1;
        }
    }
}
