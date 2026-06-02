using System.Collections.Generic;
using System.Linq;

namespace ImpactCFX
{
    /// <summary>
    /// Base class for effect assets for effects that use object pooling.
    /// </summary>
    public abstract class ImpactMultiPrefabEffectAuthoringBase : ImpactEffectAuthoringBase
    {
        /// <summary>
        /// List of prefabs to use for this effect.
        /// </summary>
        public abstract IEnumerable<PooledEffectObjectBase> Prefabs { get; }

        public override bool Validate()
        {
            IEnumerable<PooledEffectObjectBase> p = Prefabs;
            return p != null && p.Count() > 0;
        }
    }
}