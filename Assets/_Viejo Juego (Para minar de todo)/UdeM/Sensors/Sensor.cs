using System.Collections;
using System.Collections.Generic;
using UdeM.Base;
using UnityEngine;
using UnityEngine.Events;

namespace UdeM.Sensors
{
    public abstract class Sensor : CustomMonoBehaviour
    {
        /* TODO: make on change */
        protected bool _showSensorDebug;
        protected bool _isDetecting = false;
        protected int _layerMaskFilter;
        protected Vector3 _origin;
        protected GameObject _objectOrigin;
        protected GameObject _objectDetected;
        protected GameObject _objectLastDetected;
        protected Color _colorNotF = Color.red;
        protected Color _colorF = Color.green;
        protected UnityEvent _onChangeDetect = new UnityEvent();
        protected UnityEvent _onStartDetect = new UnityEvent();
        protected UnityEvent _onStopDetect = new UnityEvent();

        protected override void Awake()
        {
            base.Awake();
            _showSensorDebug = true;
            _origin = Vector3.zero;
            _layerMaskFilter = Physics.DefaultRaycastLayers;
            _objectOrigin = new GameObject("Sensor");
            _objectOrigin.transform.parent = transform;
            _objectOrigin.transform.localPosition = _origin;
        }

        protected override void Update()
        {
            base.Update();
            Sensate();
        }

        public bool isDetecting {
            get { return _isDetecting;}
        }

        public UnityEvent onDetect {
            get { return _onStartDetect; }
        }

        public UnityEvent onNotDetect {
            get { return _onStopDetect; }
        }
        public string sensorName {
            set {
                _objectOrigin.name = value;
            }
        }

        public GameObject objectDetected {
            get { return _objectDetected; }
        }

        public Color color {
            set { _colorNotF = value; }
        }

        public Vector3 origin {
            set {
                _origin = value;
                _objectOrigin.transform.localPosition = _origin;
            }
        }

        public int layerMaskFilter {
            set { _layerMaskFilter = value; }
        }

        protected Color GetDebugColor()
        {
            return (!_isDetecting) ? _colorNotF : _colorF;
        }
        protected void checkFound(GameObject obj)
        {
            bool found = obj != null;
            _objectDetected = obj;
            if (found) {
                if(_objectDetected != _objectLastDetected) {
                    _onChangeDetect.Invoke();
                }
                if(!_isDetecting) {_onStartDetect.Invoke();}
                _objectLastDetected = _objectDetected;
            } else {
                if(_isDetecting) {_onStopDetect.Invoke();}
            }
            _isDetecting = found;
        }

        protected void checkFound(bool found)
        {
            _objectDetected = null;
            _isDetecting = found;
        }
        
        private void OnDrawGizmos()
        {
            ShowSensorDebug();
        }

        protected abstract void ShowSensorDebug();
        public abstract void Sensate();
    }
}