using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Characters
{
    public class Character3DPlayerBehaviour : Character3DBehaviour
    {
		protected float _axisH;
		protected float _axisV;
		protected float _turnSmoothTime = 0.1f;
		protected float _turnSmoothVelocity;
		protected Vector3 _direction;
		protected override void Update()
		{
			base.Update();
			_axisH = Input.GetAxis("Horizontal");
			_axisV = Input.GetAxis("Vertical");
			_direction = new Vector3(_axisH, 0f, _axisV);
			if(_direction.magnitude > 0.1f && _canMove)
			{
				// Calculate the angle based in the input
				float targetAngle = Mathf.Atan2(_direction.x, _direction.z) * Mathf.Rad2Deg;
				float angle = Mathf.SmoothDampAngle(
					transform.eulerAngles.y,
					targetAngle,
					ref _turnSmoothVelocity,
					_turnSmoothTime);

				transform.rotation = Quaternion.Euler(0, angle, 0);
				_controller.Move(transform.forward * _moveSpeed * Time.deltaTime);
			} else{
				_controller.Move(transform.forward * 0 * Time.deltaTime);
			}
			if (Input.GetButtonDown("Jump") && _isGrounded) {
				Jump();
			}
		}
		protected override void Jump()
		{
			_gravityVelocity.y = Mathf.Sqrt(_jumpForce * -2 * _gravity);
		}
	}
}