using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Sensors
{
    public class SensorBool3DOverlap : Sensor
    {
        protected float _radiusSize;
        public float radiusSize {
            get { return _radiusSize; }
            set { _radiusSize = value; }
        }

        protected override void ShowSensorDebug()
        {
            Gizmos.color = GetDebugColor();
            Gizmos.DrawWireSphere(_objectOrigin.transform.position, _radiusSize);
        }


        public override void Sensate()
        {
            checkFound(Physics.CheckSphere(_objectOrigin.transform.position, _radiusSize, _layerMaskFilter));
        }
    }
}