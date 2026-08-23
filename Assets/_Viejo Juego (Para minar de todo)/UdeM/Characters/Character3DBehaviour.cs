using System.Collections;
using System.Collections.Generic;
using UdeM.Sensors;
//using UnityEditor.SceneManagement;
using UnityEngine;

namespace UdeM.Characters
{
	public class Character3DBehaviour : CharacterBehaviour
	{
		protected CharacterController _controller;
		protected float _gravity = -9.8f;
		protected Vector3 _gravityVelocity;
		protected SensorBool3DOverlap _sensorTerrain;

		protected override void Start()
		{
			base.Start();
			_controller = GetComponent<CharacterController>();
			if( _controller == null ) {
				_controller = gameObject.AddComponent<CharacterController>();
			}
			_sensorTerrain = gameObject.AddComponent<SensorBool3DOverlap>();
			_sensorTerrain.origin = new Vector3(0, transform.InverseTransformPoint(_controller.bounds.min).y, 0);
			_sensorTerrain.radiusSize = 0.12f;
			_sensorTerrain.layerMaskFilter = ~_playerLayer;
		}

		protected override void Update()
		{
			base.Update();
			_isGrounded = _sensorTerrain.isDetecting;
			if( _isGrounded && _controller.velocity.y < 0 ) {
				_gravityVelocity.y = -2f;
			}

			// Apply Gravity
			_gravityVelocity.y += _gravity * Time.deltaTime;
			_controller.Move(_gravityVelocity * Time.deltaTime);
		}

		protected override void CheckHeight() {
			RaycastHit hit;
			Physics.Raycast(transform.position, Vector3.down, out hit, Mathf.Infinity, ~_playerLayer);
			if(hit.collider != null) {
				_height = hit.distance;
			} else {
				_height = 0;
			}
		}
	}
}