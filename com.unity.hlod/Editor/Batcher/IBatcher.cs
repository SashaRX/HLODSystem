using System;
using Unity.HLODSystem.Utils;
using UnityEngine;

namespace Unity.HLODSystem{public interface IBatcher : IDisposable{
        void Batch(Transform rootTransform, DisposableList<HLODBuildInfo> targets, bool castShadows, Action<float> onProgress);
    }
}
