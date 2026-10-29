using System.Collections;
using System.Collections.Generic;
using UdeM.Base;
using UnityEngine;


namespace UdeM.Sensors
{
    public class SensorGameObject2DRaycast : Sensor
    {
        protected Vector3 _direction;
        protected float _size;
        protected RaycastHit2D _hit;

        protected override void Awake()
        {
            base.Awake();
            _direction = new Vector3(1,0,0);
            _size = 1.0f;
            origin = new Vector3(0,1,0);
        }

        public Vector3 direction {
            set { _direction = value; }
        }

        public float size {
            set { _size = value; }
        }

        protected override void ShowSensorDebug()
        {
            Gizmos.color = GetDebugColor();
            Gizmos.DrawLine(_objectOrigin.transform.position, GetDirectionDirection() * _size);
        }

        public override void Sensate()
        {
            _hit = Physics2D.Raycast(_objectOrigin.transform.position, GetDirectionDirection(), _size, _layerMaskFilter);
            checkFound(_hit.collider == null ? null : _hit.collider.gameObject);
            if (_showSensorDebug) {
                ShowSensorDebug();
            }
        }

        private Vector2 GetDirectionDirection()
        {
            return new Vector2(_direction.x * transform.localScale.x, _direction.y);
        }
    }


}