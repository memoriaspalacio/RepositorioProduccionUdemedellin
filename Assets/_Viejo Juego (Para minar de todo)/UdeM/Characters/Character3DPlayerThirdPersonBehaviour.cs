using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Characters
{
    public class Character3DPlayerThirdPersonBehaviour : Character3DBehaviour
    {
		protected Transform cameraTransform;
		protected float _axisH;
		protected float _axisV;
		protected float _turnSmoothTime = 0.1f;
		protected float _turnSmoothVelocity;
		protected Vector3 _direction;

        protected override void Awake()
        {
            base.Awake();
			_jumpForce = 2.5f;
        }

        protected override void Start()
        {
            base.Start();
			cameraTransform = Camera.main.transform;
        }

        protected override void Update()
		{
			base.Update();
			if (_isGrounded){
				SetDirection();
			}
			
			if(_isGrounded && _direction.magnitude > 0.1f && _canMove){
				SetRotation();
				ApplyMove();
			} else if(!_canMove) {
				_controller.Move(Vector3.zero);
			}

			if (!_isGrounded){
				ApplyMoveOnAir();
			}

			if (Input.GetButtonDown("Jump") && _isGrounded) {
				Jump();
			}
		}

		protected virtual void SetDirection()
		{
			_axisH = Input.GetAxis("Horizontal");
            _axisV = Input.GetAxis("Vertical");

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            _direction = camForward * _axisV + camRight * _axisH;
		}

		protected virtual void ApplyMove() {
			_controller.Move(transform.forward * _moveSpeed * Time.deltaTime);
		}

		protected virtual void ApplyMoveOnAir() {
			 _controller.Move(_direction.normalized * _moveSpeed * Time.deltaTime);
		}

		protected virtual void SetRotation() 
		{
			float targetAngle = Mathf.Atan2(_direction.x, _direction.z) * Mathf.Rad2Deg;
			float angle = Mathf.SmoothDampAngle(
				transform.eulerAngles.y,
				targetAngle,
				ref _turnSmoothVelocity,
				_turnSmoothTime);

			transform.rotation = Quaternion.Euler(0, angle, 0);
		}

		protected override void Jump()
		{
			_gravityVelocity.y = Mathf.Sqrt(_jumpForce * -2 * _gravity);
		}
	}
}