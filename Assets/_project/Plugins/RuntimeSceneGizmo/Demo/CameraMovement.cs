using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RuntimeSceneGizmo
{
	public class CameraMovement : MonoBehaviour
	{
#pragma warning disable 0649
		[SerializeField]
		private float sensitivity = 0.5f;
#pragma warning restore 0649

		private Vector3 prevMousePos;
		private Transform mainCamParent;
		
		[SerializeField] private bool requireTwoFingerRotation = false;
		private float minPitch = -89.8f;
		private float maxPitch = 89.8f;

		private float yaw;
		private float pitch;
		public static bool IsCameraLocked { get; set; } = false;
		public static Action<bool> OnCameraLockStateChanged;
		public static Action<Vector3, Vector3, float> MoveCameraToTransform;

		private void OnEnable()
		{
			OnCameraLockStateChanged += SetCameraLockState;
			MoveCameraToTransform += MoveToPositionAndRotation;
		}

		private void OnDisable()
		{
			OnCameraLockStateChanged -= SetCameraLockState;
			MoveCameraToTransform -= MoveToPositionAndRotation;
		}

		private void SetCameraLockState(bool locked)
		{
			locked = false;
			IsCameraLocked = locked;
		}

		private void MoveToPositionAndRotation(Vector3 targetPos, Vector3 targetRot, float duration)
		{
			if (mainCamParent == null && Camera.main != null)
				mainCamParent = Camera.main.transform.parent;

			Transform targetTransform = mainCamParent != null ? mainCamParent : (Camera.main != null ? Camera.main.transform : null);
			if (targetTransform == null) return;

			targetTransform.DOMove(targetPos, duration).SetEase(Ease.InOutQuad);
			targetTransform.DORotate(targetRot, duration).SetEase(Ease.InOutQuad);

			yaw = targetRot.y;
			pitch = targetRot.x;
		}

		private void Awake()
		{
			mainCamParent = Camera.main != null ? Camera.main.transform.parent : null;
		}

		private void Update()
		{
			if (IsCameraLocked) return;
			if (IsPointerOverUI()) return;

			if (Input.touchCount > 0) HandleTouchInput();
			else HandleMouseInput();
		}

		private void HandleMouseInput()
		{
			if( Input.GetMouseButtonDown( 0 ) )
				prevMousePos = Input.mousePosition;
			else if( Input.GetMouseButton( 0 ) )
			{
				// Vector3 mousePos = Input.mousePosition;
				// Vector2 deltaPos = ( mousePos - prevMousePos ) * sensitivity;
				//
				// Vector3 rot = mainCamParent.localEulerAngles;
				// while( rot.x > 180f )
				// 	rot.x -= 360f;
				// while( rot.x < -180f )
				// 	rot.x += 360f;
				//
				// rot.x = Mathf.Clamp( rot.x - deltaPos.y, -89.8f, 89.8f );
				// rot.y += deltaPos.x;
				// rot.z = 0f;
				//
				// mainCamParent.localEulerAngles = rot;
				// prevMousePos = mousePos;
				
				Vector3 mousePos = Input.mousePosition;
				Vector2 deltaPos = (mousePos - prevMousePos) * sensitivity;

				RotateCamera(deltaPos);

				prevMousePos = mousePos;
			}
		}

		private void HandleTouchInput()
		{
			Vector2 delta;

			if (requireTwoFingerRotation)
			{
				if (Input.touchCount != 2)
					return;

				Touch touch0 = Input.GetTouch(0);
				Touch touch1 = Input.GetTouch(1);

				if (touch0.phase == TouchPhase.Began ||
				    touch1.phase == TouchPhase.Began)
					return;

				delta = (touch0.deltaPosition + touch1.deltaPosition) * 0.5f;
			}
			else
			{
				if (Input.touchCount != 1)
					return;

				Touch touch = Input.GetTouch(0);

				if (touch.phase != TouchPhase.Moved)
					return;

				delta = touch.deltaPosition;
			}

			RotateCamera(delta);
		}
		
		private void RotateCamera(Vector2 delta)
		{
			yaw += delta.x * sensitivity;
			pitch -= delta.y * sensitivity;
			
			pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

			mainCamParent.localRotation =
				Quaternion.Euler(pitch, yaw, 0f);
		}
		
		bool _isDragStartedOnUI = false;
		
		bool IsPointerOverUI()
		{
			var eventSystem = EventSystem.current;
			if (eventSystem == null)
				return false;

			// 1. Mobile Touches
			if (Input.touchCount > 0)
			{
				Touch touch = Input.GetTouch(0);

				if (touch.phase == TouchPhase.Began)
				{
					_isDragStartedOnUI = eventSystem.IsPointerOverGameObject(touch.fingerId);
				}
				else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
				{
					_isDragStartedOnUI = false;
					return false;
				}

				return _isDragStartedOnUI;
			}

			// 2. Mouse / Editor / PC
			if (Input.GetMouseButtonDown(0))
			{
				_isDragStartedOnUI = eventSystem.IsPointerOverGameObject();
			}
			else if (Input.GetMouseButtonUp(0))
			{
				_isDragStartedOnUI = false;
				return false;
			}
            
			if (Input.GetMouseButton(0)) return _isDragStartedOnUI;

			return eventSystem.IsPointerOverGameObject();
		}
	}
}