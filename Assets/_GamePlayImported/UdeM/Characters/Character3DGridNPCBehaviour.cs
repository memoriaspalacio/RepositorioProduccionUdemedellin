using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Characters
{
    public class Character3DGridNPCBehaviour : CharacterBehaviour
    {
        [Header("Cuadrícula")]
        [SerializeField] private MeshGrid grid;
        [SerializeField] private float heightOffset = 1f;

        [Header("Movimiento")]
        [SerializeField] private float gridMoveSpeed = 5f;
        [SerializeField] private bool rotateTowardsMovement = true;

        [Header("Patrulla")]
        [SerializeField] private List<Transform> patrolPoints;
        [SerializeField] private float patrolPointWaitTime = 2f;

        [Header("Ritmo")]
        [SerializeField] private bool requireRhythm = true;
        [SerializeField] private bool rhythmWindowOpen = true;

        [Header("Estado")]
        [SerializeField] private Vector2Int currentCell;
        [SerializeField] private bool isMoving;

        private CharacterController controller;
        private GameObject target;
        private int patrolIndex;
        private float nextPatrolTime;
        private bool movementConsumedThisWindow;

        public Vector2Int CurrentCell => currentCell;
        public bool IsMoving => isMoving;
        public bool RhythmWindowOpen => rhythmWindowOpen;

        protected override void Awake()
        {
            base.Awake();

            controller = GetComponent<CharacterController>();

            if (controller == null)
                controller = gameObject.AddComponent<CharacterController>();
        }

        protected override void Start()
        {
            base.Start();

            if (grid == null)
                grid = FindFirstObjectByType<MeshGrid>();

            if (grid == null)
            {
                Debug.LogError(
                    "No se encontró un objeto con el componente MeshGrid.",
                    this
                );

                enabled = false;
                return;
            }

            currentCell = grid.WorldToCell(transform.position);
            transform.position = grid.CellToWorldCenter(
                currentCell,
                heightOffset
            );

            Transform vision = transform.Find("Vision");

            if (vision != null)
            {
                GridVisionBehaviour visionBehaviour =
                    vision.GetComponent<GridVisionBehaviour>();

                if (visionBehaviour == null)
                    visionBehaviour =
                        vision.gameObject.AddComponent<GridVisionBehaviour>();

                visionBehaviour.Initialize(this);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (grid == null || isMoving || !CanMoveOnRhythm())
                return;

            Vector2Int targetCell = GetCurrentTargetCell();

            if (targetCell == currentCell)
            {
                HandleReachedTarget();
                return;
            }

            Vector2Int direction = GetDirectionToCell(targetCell);
            TryMove(direction);
        }

        private Vector2Int GetCurrentTargetCell()
        {
            if (target != null)
                return grid.WorldToCell(target.transform.position);

            if (patrolPoints == null || patrolPoints.Count == 0)
                return currentCell;

            if (Time.time < nextPatrolTime)
                return currentCell;

            Transform patrolPoint = patrolPoints[patrolIndex];

            if (patrolPoint == null)
                return currentCell;

            return grid.WorldToCell(patrolPoint.position);
        }

        private Vector2Int GetDirectionToCell(Vector2Int targetCell)
        {
            Vector2Int difference = targetCell - currentCell;

            if (Mathf.Abs(difference.x) >= Mathf.Abs(difference.y))
            {
                return difference.x > 0
                    ? Vector2Int.right
                    : Vector2Int.left;
            }

            return difference.y > 0
                ? Vector2Int.up
                : Vector2Int.down;
        }

        private bool TryMove(Vector2Int direction)
        {
            if (direction == Vector2Int.zero || isMoving)
                return false;

            if (!CanMoveOnRhythm())
                return false;

            if (!grid.TryGetNeighbour(
                    currentCell,
                    direction,
                    out Vector2Int nextCell))
            {
                return false;
            }

            ConsumeRhythmMovement();

            StartCoroutine(MoveToCell(
                nextCell,
                direction
            ));

            return true;
        }

        private IEnumerator MoveToCell(
            Vector2Int targetCell,
            Vector2Int direction)
        {
            isMoving = true;
            _canMove = false;

            Vector3 targetPosition = grid.CellToWorldCenter(
                targetCell,
                heightOffset
            );

            if (rotateTowardsMovement)
            {
                Vector3 lookDirection = new Vector3(
                    direction.x,
                    0f,
                    direction.y
                );

                if (lookDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(
                        lookDirection,
                        Vector3.up
                    );
                }
            }

            while (Vector3.Distance(
                       transform.position,
                       targetPosition) > 0.01f)
            {
                Vector3 nextPosition = Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    gridMoveSpeed * Time.deltaTime
                );

                controller.Move(
                    nextPosition - transform.position
                );

                yield return null;
            }

            controller.Move(
                targetPosition - transform.position
            );

            currentCell = targetCell;
            isMoving = false;
            _canMove = true;

            HandleReachedTarget();
        }

        private void HandleReachedTarget()
        {
            if (target != null)
                return;

            if (patrolPoints == null || patrolPoints.Count == 0)
                return;

            Transform patrolPoint = patrolPoints[patrolIndex];

            if (patrolPoint == null)
            {
                AdvancePatrolPoint();
                return;
            }

            Vector2Int patrolCell =
                grid.WorldToCell(patrolPoint.position);

            if (currentCell != patrolCell)
                return;

            AdvancePatrolPoint();
            nextPatrolTime = Time.time + patrolPointWaitTime;
        }

        private void AdvancePatrolPoint()
        {
            patrolIndex++;

            if (patrolIndex >= patrolPoints.Count)
                patrolIndex = 0;
        }

        private bool CanMoveOnRhythm()
        {
            if (!requireRhythm)
                return true;

            return rhythmWindowOpen &&
                   !movementConsumedThisWindow;
        }

        private void ConsumeRhythmMovement()
        {
            if (requireRhythm)
                movementConsumedThisWindow = true;
        }

        public void SetRhythmWindow(bool isOpen)
        {
            rhythmWindowOpen = isOpen;

            if (isOpen)
                movementConsumedThisWindow = false;
        }

        public void SetRequireRhythm(bool value)
        {
            requireRhythm = value;
            movementConsumedThisWindow = false;
        }

        public void PlayerDetected(GameObject detectedPlayer)
        {
            target = detectedPlayer;
        }

        public void PlayerLost(GameObject lostPlayer)
        {
            if (target == lostPlayer)
                target = null;
        }

        protected override void CheckHeight()
        {
            if (Physics.Raycast(
                    transform.position,
                    Vector3.down,
                    out RaycastHit hit,
                    Mathf.Infinity,
                    ~_playerLayer,
                    QueryTriggerInteraction.Ignore))
            {
                _height = hit.distance;
                _isGrounded = true;
            }
            else
            {
                _height = 0f;
                _isGrounded = false;
            }
        }

        private class GridVisionBehaviour : MonoBehaviour
        {
            private Character3DGridNPCBehaviour owner;

            public void Initialize(
                Character3DGridNPCBehaviour npcOwner)
            {
                owner = npcOwner;
            }

            private void OnTriggerStay(Collider other)
            {
                if (owner != null &&
                    other.CompareTag("Player"))
                {
                    owner.PlayerDetected(other.gameObject);
                }
            }

            private void OnTriggerExit(Collider other)
            {
                if (owner != null &&
                    other.CompareTag("Player"))
                {
                    owner.PlayerLost(other.gameObject);
                }
            }
        }
    }
}