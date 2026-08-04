using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace UdeM.Characters {
    public class Character3DNavMeshNPCBehaviour : Character3DNavMeshBehaviour
    {
        [SerializeField] protected List<Transform> _patrolPoints;
        protected GameObject _visionObject;
        protected int _patrolIterator;
        protected float _patrolPointTime;
        protected int _actionState;
        protected const int PATROLLING = 2;
        protected const int ATTACKING = 3;

        protected override void Awake()
        {
            base.Awake();
            _actionState = PATROLLING;
            _patrolIterator = 0;
            _patrolPointTime = 2f;
        }

        protected override void Start()
        {
            base.Start();
            _visionObject = transform.Find("Vision").gameObject;
            if(_visionObject != null) {
                _visionObject.AddComponent<VisionBehavior>();
            }
            Patrol();
        }

        protected override void OnFinishMove()
        {
            base.OnFinishMove();
            if(_actionState == PATROLLING) {
                _patrolIterator++;
                _patrolIterator = (_patrolIterator >= _patrolPoints.Count) ? 0 : _patrolIterator;
                if(_patrolPointTime <= 0) {
                    Patrol();
                } else{
                    StartCoroutine(WaitForPatrol(_patrolPointTime));
                }
                
            }
        }

        protected virtual IEnumerator WaitForPatrol(float time) {
            yield return new WaitForSeconds(time);
            Patrol();
        }

        protected void Patrol() {
            if(_patrolPoints.Count < 2) {
                Debug.LogWarning("To patrol, it must be more than 2 patrol points");
                return;
            }
            _actionState = PATROLLING;
            GoToDestination(_patrolPoints[_patrolIterator].position);
        }

        public virtual void PlayerDetected(GameObject target) {
           _actionState = ATTACKING;
           _target = target;
           Attack();
        }

        protected virtual void Attack() {
            if(_target != null) {
                GoToDestination(_target.transform.position);
            }
            
        }

        protected class VisionBehavior : MonoBehaviour {

            void OnTriggerStay(Collider other) {
                if(other.tag == "Player") {
                    transform.parent.GetComponent<Character3DNavMeshNPCBehaviour>().PlayerDetected(other.gameObject);
                }
            }

            void OnTriggerExit(Collider other) {
                if(other.tag == "Player") {
                    transform.parent.GetComponent<Character3DNavMeshNPCBehaviour>().Patrol();
                }
            }
        }
    }
}