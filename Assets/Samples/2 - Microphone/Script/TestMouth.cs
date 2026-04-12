using UnityEngine;
using UniVRM10;
using UnityEngine.InputSystem;

public class TestMouth : MonoBehaviour
{
    private Vrm10Instance vrmInstance;

    void Start()
    {
        vrmInstance = GetComponent<Vrm10Instance>();
    }

    void Update()
    {
        if (vrmInstance != null && vrmInstance.Runtime != null && Keyboard.current != null)
        {
            // --- THE MOUTH (Spacebar) ---
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.Aa, 1.0f);

            if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.Aa, 0.0f);


            // --- HAPPY FACE (Number 1 Key) ---
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.Happy, 1.0f);

            if (Keyboard.current.digit1Key.wasReleasedThisFrame)
                vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.Happy, 0.0f);


            // --- ANGRY FACE (Number 2 Key) ---
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.Angry, 1.0f);

            if (Keyboard.current.digit2Key.wasReleasedThisFrame)
                vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.Angry, 0.0f);
        }
    }
}