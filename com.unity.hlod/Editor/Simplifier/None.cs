using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.HLODSystem.Simplifier{
    class None : ISimplifier{
        public None(SerializableDynamicObject simplifierOptions){
        }
        [InitializeOnLoadMethod]
        static void RegisterType(){
            //This simplifier should be first always.
            SimplifierTypes.RegisterType(typeof(None), -1);
        }

        public IEnumerator Simplify(HLODBuildInfo info){
            yield break;
        }

        public void SimplifyImmidiate(HLODBuildInfo buildInfo){
        }
    }
}
