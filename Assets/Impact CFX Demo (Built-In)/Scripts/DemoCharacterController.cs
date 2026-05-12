using ImpactCFX;
using UnityEngine;
using UnityEngine.UI;

namespace ImpactCFXDemo
{
    public class DemoCharacterController : MonoBehaviour
    {
        [SerializeField]
        private Rigidbody characterRigidbody;

        [Header("Movement")]
        [SerializeField]
        private float moveSpeed = 10;
        [SerializeField]
        private float sprintMultiplier = 2;
        [SerializeField]
        private float sneakMultiplier = 0.5f;
        [SerializeField]
        private float movementSmoothing = 0.1f;

        [Header("Camera")]
        [SerializeField]
        private Vector3 cameraOffset;
        [SerializeField]
        private Transform cameraTransform;
        [SerializeField]
        private float sensitivity = 1;
        [SerializeField]
        private float cameraSmoothing = 0.1f;

        [Header("Interaction")]
        [SerializeField]
        private float interactionDistance = 10f;
        [SerializeField]
        private float interactionOriginDistance = 0.2f;
        [SerializeField]
        private float pickedUpDrag = 20f;
        [SerializeField]
        private float pickupHoldForce = 20f;
        [SerializeField]
        private float throwForce = 20f;

        [Header("Footsteps")]
        [SerializeField]
        private ImpactTag footstepLeftTag;
        [SerializeField]
        private ImpactTag footstepRightTag;
        [SerializeField]
        private float footstepInterval;

        [Header("Weapon")]
        [SerializeField]
        private ImpactTag bulletTag;
        [SerializeField]
        private float bulletForce;

        [Header("UI")]
        [SerializeField]
        private Image crosshairImage;

        private Vector2 targetCharacterRotation = Vector2.zero;
        private Vector2 smoothedCharacterRotation, smoothedCharacterRotationV;

        private Vector3 targetMovement = Vector2.zero;
        private Vector3 smoothedMovement, smoothedMovementV;

        private bool isSneaking;

        private Vector3 previousPosition;
        private float distanceTravelled;

        private Rigidbody pickedUpObject;
        private float pickedUpObjectDistance;
        private Quaternion pickedUpObjectRotationOffset;

        private int foot = 1;

        private Vector3 interactionRayOrigin => cameraTransform.position + cameraTransform.forward * interactionOriginDistance;


        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        #region Updates

        private void Update()
        {
            targetMovement = getMovementInput();

            Vector2 mouseDelta = new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X")) * sensitivity;
            targetCharacterRotation += mouseDelta;
            targetCharacterRotation.x = Mathf.Clamp(targetCharacterRotation.x, -80, 80);

            smoothedCharacterRotation = Vector2.SmoothDamp(smoothedCharacterRotation, targetCharacterRotation, ref smoothedCharacterRotationV, cameraSmoothing);
            smoothedMovement = Vector3.SmoothDamp(smoothedMovement, targetMovement, ref smoothedMovementV, movementSmoothing);


            cameraTransform.position = transform.position + cameraOffset;
            cameraTransform.rotation = Quaternion.Euler(smoothedCharacterRotation.x, smoothedCharacterRotation.y, 0);

            if (pickedUpObject == null && isHoveredOverObject())
            {
                crosshairImage.color = new Color(1, 1, 1, 0.5f);
            }
            else
            {
                crosshairImage.color = new Color(1, 1, 1, 0f);
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (pickedUpObject == null)
                    triggerWeapon();
                else
                    dropObject(throwForce);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (pickedUpObject == null)
                {
                    if (!pickupObject())
                    {
                        pressButton();
                    }
                }
                else
                    dropObject(0);
            }
        }

        private void FixedUpdate()
        {
            characterRigidbody.MoveRotation(Quaternion.Euler(0, smoothedCharacterRotation.y, 0));
            characterRigidbody.linearVelocity = new Vector3(smoothedMovement.x, characterRigidbody.linearVelocity.y, smoothedMovement.z);

            updatePickedUpObject();
        }

        private void LateUpdate()
        {
            Vector3 currentPosition = transform.position;
            float distanceTravelledThisFrame = Vector3.Distance(previousPosition, currentPosition);

            distanceTravelled += distanceTravelledThisFrame;

            if (distanceTravelled > footstepInterval)
            {
                distanceTravelled = 0;
                foot = -foot;

                triggerFootstep();
            }

            previousPosition = currentPosition;
        }

        #endregion

        #region Impact Footstep and Bullet Impact Integration

        private void triggerFootstep()
        {
            //Velocity is a 0-1 value used to scale the volume of the footstep sounds
            float velocity = isSneaking ? 0.25f : 1;

            RaycastHit hit;
            Ray r = new Ray(transform.position + new Vector3(0, 0.01f, 0), Vector3.down);
            if (Physics.Raycast(r, out hit))
            {
                if (foot > 0)
                {
                    ImpactRaycaster.QueueRaycastInverted3D(hit, Vector3.down * velocity, footstepRightTag.ToTagMask(), CollisionType.Collision, 0, 2);
                }
                else if (foot < 0)
                {
                    ImpactRaycaster.QueueRaycastInverted3D(hit, Vector3.down * velocity, footstepLeftTag.ToTagMask(), CollisionType.Collision, 0, 2);
                }
            }
        }

        private void triggerWeapon()
        {
            RaycastHit hit;
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit))
            {
                ImpactRaycaster.QueueRaycastInverted3D(hit, cameraTransform.forward * bulletForce, bulletTag.ToTagMask(), CollisionType.Collision, 0, 1);

                if (hit.rigidbody != null)
                    hit.rigidbody.AddForceAtPosition(cameraTransform.forward * bulletForce, hit.point);
            }
        }

        #endregion

        #region Objects and Buttons

        private bool isHoveredOverObject()
        {
            RaycastHit hit;
            if (Physics.Raycast(interactionRayOrigin, cameraTransform.forward, out hit, interactionDistance))
            {
                DemoButton button = hit.collider.GetComponentInParent<DemoButton>();
                DemoInteractiveObject interactiveObject = hit.collider.GetComponentInParent<DemoInteractiveObject>();

                if (interactiveObject != null || button != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool pickupObject()
        {
            RaycastHit hit;
            if (Physics.Raycast(interactionRayOrigin, cameraTransform.forward, out hit, interactionDistance))
            {
                DemoInteractiveObject interactiveObject = hit.collider.GetComponentInParent<DemoInteractiveObject>();

                if (interactiveObject != null)
                {
                    pickedUpObjectDistance = hit.distance;
                    pickedUpObject = hit.rigidbody;
                    pickedUpObject.useGravity = false;
                    pickedUpObject.linearDamping = pickedUpDrag;
                    pickedUpObject.angularDamping = pickedUpDrag;
                    pickedUpObjectRotationOffset = Quaternion.Inverse(cameraTransform.rotation) * hit.collider.transform.rotation;
                    return true;
                }
            }

            return false;
        }

        private void updatePickedUpObject()
        {
            if (pickedUpObject != null)
            {
                Vector3 target = cameraTransform.position + cameraTransform.forward * pickedUpObjectDistance;
                Vector3 dir = target - pickedUpObject.position;

                pickedUpObject.AddForce(dir * pickupHoldForce, ForceMode.VelocityChange);
            }
        }

        private void dropObject(float force)
        {
            if (pickedUpObject != null)
            {
                pickedUpObject.useGravity = true;
                pickedUpObject.linearDamping = 0;
                pickedUpObject.angularDamping = 0.05f;
                pickedUpObject.AddForce(cameraTransform.forward * force, ForceMode.VelocityChange);
                pickedUpObject = null;
            }
        }

        private bool pressButton()
        {
            RaycastHit hit;

            if (Physics.Raycast(interactionRayOrigin, cameraTransform.forward, out hit, interactionDistance))
            {
                DemoButton button = hit.collider.GetComponentInParent<DemoButton>();

                if (button != null)
                {
                    button.Press();
                }
            }

            return false;
        }

        #endregion

        #region Movement

        private Vector3 getMovementInput()
        {
            Vector3 movementInput = Vector3.zero;

            if (Input.GetKey(KeyCode.W))
                movementInput.z = 1;
            else if (Input.GetKey(KeyCode.S))
                movementInput.z = -1;

            if (Input.GetKey(KeyCode.A))
                movementInput.x = -1;
            else if (Input.GetKey(KeyCode.D))
                movementInput.x = 1;

            movementInput.Normalize();
            movementInput *= moveSpeed;

            if (Input.GetKey(KeyCode.LeftShift))
                movementInput *= sprintMultiplier;

            isSneaking = Input.GetKey(KeyCode.LeftControl);
            if (isSneaking)
                movementInput *= sneakMultiplier;

            return transform.rotation * movementInput;
        }

        #endregion
    }
}

