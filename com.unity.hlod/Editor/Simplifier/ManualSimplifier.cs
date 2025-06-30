using System.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.HLODSystem.Simplifier {
    public class ManualSimplifier : ISimplifier {
        public ManualSimplifier(SerializableDynamicObject simplifierOptions) { }

        [InitializeOnLoadMethod]
        static void RegisterType() {
            SimplifierTypes.RegisterType(typeof(ManualSimplifier), 100);
        }

        public IEnumerator Simplify(HLODBuildInfo buildInfo) {
            // Пропускаем автоматическое упрощение
            yield break;
        }

        public void SimplifyImmidiate(HLODBuildInfo buildInfo) {
            // Не выполняем упрощение
        }

        public static void OnGUI(SerializableDynamicObject simplifierOptions) {
            EditorGUILayout.HelpBox(
                "Manual simplification mode.\n" +
                "Meshes will be exported without automatic simplification.",
                MessageType.Info);
        }
    }
}
