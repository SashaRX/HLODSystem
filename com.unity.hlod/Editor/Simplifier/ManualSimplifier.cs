namespace Unity.HLODSystem.Simplifier{
    public class ManualSimplifier : ISimplifier{
        public ManualSimplifier(SerializableDynamicObject simplifierOptions) { }

        [InitializeOnLoadMethod]
        static void RegisterType(){
            SimplifierTypes.RegisterType(typeof(ManualSimplifier), 100);
        }

        public IEnumerator Simplify(HLODBuildInfo buildInfo){
            // Пропускаем упрощение - меши остаются как есть
            yield break;
        }

        public void SimplifyImmidiate(HLODBuildInfo buildInfo){
            // Ничего не делаем
        }

        public static void OnGUI(SerializableDynamicObject simplifierOptions){
            EditorGUILayout.HelpBox(
                "This simplifier does nothing. Meshes will be used as-is from the batcher.",
                MessageType.Info);
        }
    }
}
