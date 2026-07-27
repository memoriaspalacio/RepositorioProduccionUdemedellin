using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace UdeM.Characters
{
    public class Character3DNavMeshBehaviour : CharacterBehaviour
    {
        protected NavMeshAgent _navigator;
        protected UnityEvent _onStartMove;
        protected UnityEvent _onFinishMove;
        protected GameObject _target;
        [SerializeField]protected int _state;
        protected const int STANDBY = 0;
        protected const int MOVING = 1;

        protected override void Start()
        {
            base.Start();
            _onStartMove = new UnityEvent();
            _onFinishMove = new UnityEvent();
            _onFinishMove.AddListener(OnFinishMove);
            _navigator = GetComponent<NavMeshAgent>();
            if(_navigator == null) {
                _navigator = gameObject.AddComponent<NavMeshAgent>();
            }
        }

        protected virtual void OnFinishMove() {
            _state = STANDBY;
        }

        protected override void Update()
        {
            base.Update();
            CheckMoveState();
        }

        protected void CheckMoveState() {
            if(_state == MOVING) {
                if(!_navigator.pathPending && _navigator.remainingDistance <= _navigator.stoppingDistance) {
                    if(!_navigator.hasPath && _navigator.velocity.sqrMagnitude == 0) {
                        _onFinishMove.Invoke();
                    }
                }
            }
        }

        protected float speed {
            set { _navigator.speed = value; }
        }

        protected float acceleration {
            set { _navigator.acceleration = value; }
        }

        protected float angularSpeed {
            set { _navigator.angularSpeed = value; }
        }

        protected float stoppingDistance {
            set { _navigator.stoppingDistance = value; }
        }

        protected bool autoBraking {
            set { _navigator.autoBraking = value; }
        }

        public void GoToDestination(Vector3 position) {
            _state = MOVING;
            _onStartMove.Invoke();
            _navigator.destination = position;
        }

        protected override void CheckHeight()
        {
            
        }
    }
}