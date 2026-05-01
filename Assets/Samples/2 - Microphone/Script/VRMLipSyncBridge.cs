using UnityEngine;
using UniVRM10;
using System.Collections.Generic;

namespace uLipSync
{
    public class VRMLipSyncBridge : MonoBehaviour
    {
        [Header("VRM Setup")]
        public Vrm10Instance vrmInstance;

        [Header("LipSync Smoothing Settings")]
        public float minVolume = -2.5f;
        public float maxVolume = -1.5f;
        [Range(0f, 0.3f)] public float smoothness = 0.05f;

        [Header("Mouth Open Limits (0.0 to 1.0)")]
        [Range(0f, 1f)] public float limitA = 1.0f;
        [Range(0f, 1f)] public float limitI = 1.0f;
        [Range(0f, 1f)] public float limitU = 1.0f;
        [Range(0f, 1f)] public float limitE = 1.0f;
        [Range(0f, 1f)] public float limitO = 1.0f;
        [Range(0f, 1f)] public float limitFV = 1.0f;
        [Range(0f, 1f)] public float limitSH = 1.0f;

        // Internal tracking variables
        private LipSyncInfo _info = new LipSyncInfo();
        private bool _lipSyncUpdated = false;
        private float _volume = 0f;
        private float _openCloseVelocity = 0f;

        // Tracking arrays for the 7 VRM Phonemes (5 Vowels + 2 Consonants)
        private float[] _vowelWeights = new float[7];
        private float[] _vowelVelocities = new float[7];
        private readonly string[] _vowelKeys = { "A", "I", "U", "E", "O", "FV", "SH" };
        private readonly ExpressionKey[] _expressionKeys = {
            ExpressionKey.Aa,
            ExpressionKey.Ih,
            ExpressionKey.Ou,
            ExpressionKey.Ee,
            ExpressionKey.Oh,
            ExpressionKey.CreateCustom("FV"), // Maps to your new custom FV clip!
            ExpressionKey.CreateCustom("SH")  // Maps to your new custom SH clip!
        };

        // uLipSync feeds audio data into this function
        public void OnLipSyncUpdate(LipSyncInfo info)
        {
            _info = info;
            _lipSyncUpdated = true;
        }

        void LateUpdate()
        {
            if (vrmInstance == null || vrmInstance.Runtime == null) return;

            // 1. Run the math from the original uLipSync script
            UpdateVolume();
            UpdateVowels();

            // Store your custom limits in an array to match the loop
            float[] shapeLimits = { limitA, limitI, limitU, limitE, limitO, limitFV, limitSH };

            // 2. Apply the smoothed math to VRM 1.0 (Scale of 0.0 to 1.0)
            for (int i = 0; i < 7; i++)
            {
                // Multiply the vowel shape by how loud the audio is AND your custom limit slider
                float finalWeight = _vowelWeights[i] * _volume * shapeLimits[i];
                vrmInstance.Runtime.Expression.SetWeight(_expressionKeys[i], finalWeight);
            }

            _lipSyncUpdated = false;
        }

        void UpdateVolume()
        {
            float normVol = 0f;
            if (_lipSyncUpdated && _info.rawVolume > 0f)
            {
                normVol = Mathf.Log10(_info.rawVolume);
                normVol = (normVol - minVolume) / Mathf.Max(maxVolume - minVolume, 1e-4f);
                normVol = Mathf.Clamp(normVol, 0f, 1f);
            }
            _volume = Mathf.SmoothDamp(_volume, normVol, ref _openCloseVelocity, smoothness);
        }

        void UpdateVowels()
        {
            float sum = 0f;
            var ratios = _info.phonemeRatios;

            for (int i = 0; i < 7; i++)
            {
                float targetWeight = 0f;
                if (ratios != null && ratios.TryGetValue(_vowelKeys[i], out targetWeight))
                {
                    // Found ratio
                }
                else
                {
                    targetWeight = (_vowelKeys[i] == _info.phoneme) ? 1f : 0f;
                }

                _vowelWeights[i] = Mathf.SmoothDamp(_vowelWeights[i], targetWeight, ref _vowelVelocities[i], smoothness);
                sum += _vowelWeights[i];
            }

            // Normalize weights so the mouth doesn't over-stretch
            for (int i = 0; i < 7; i++)
            {
                _vowelWeights[i] = sum > 0f ? _vowelWeights[i] / sum : 0f;
            }
        }
    }
}