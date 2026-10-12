using System;
using System.Collections.Generic;
using System.Linq;
using LlamAcademy.Dinos.Utility;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Action = Unity.Behavior.Action;
using Random = UnityEngine.Random;

namespace LlamAcademy.Dinos.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Delayed Patrol",
        story: "[Self] patrols between [Waypoints] , roaming briefly between each stop.", category: "Action/Navigation",
        id: "650bf3c61b07a4c8ad7f2df73a85dae9")]
    public partial class DelayedPatrolAction : Action
    {
        [FormerlySerializedAs("Agent")] [SerializeReference]
        public BlackboardVariable<GameObject> Self;

        [SerializeReference] public BlackboardVariable<List<Vector3>> Waypoints;
        [SerializeReference] public BlackboardVariable<Vector2> WaypointRoamTime = new(new Vector2(3, 6));

        private NavMeshAgent NavMeshAgent;
        private Animator Animator;

        private List<Vector3> NavMeshWaypoints;
        private Vector3 CurrentTarget;
        private int CurrentPatrolPoint;
        private bool IsWaiting;
        private float WaitingTimer;

        protected override Status OnStart()
        {
            if (Self.Value == null || !Self.Value.TryGetComponent(out NavMeshAgent))
            {
                return Status.Failure;
            }

            Animator = Self.Value.GetComponentInChildren<Animator>();

            IsWaiting = false;
            WaitingTimer = 0.0f;

            PickNavMeshWaypoints();

            if (NavMeshWaypoints == null || NavMeshWaypoints.Count == 0)
            {
                return Status.Failure;
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Self.Value == null || NavMeshAgent == null)
            {
                return Status.Failure;
            }

            if (NavMeshWaypoints == null || NavMeshWaypoints.Count == 0)
            {
                PickNavMeshWaypoints();
                if (NavMeshWaypoints == null || NavMeshWaypoints.Count == 0)
                {
                    return Status.Failure;
                }
            }

            if (IsWaiting)
            {
                if (WaitingTimer > 0.0f)
                {
                    WaitingTimer -= Time.deltaTime;
                }
                else
                {
                    WaitingTimer = 0f;
                    IsWaiting = false;
                    MoveToNextWaypoint();
                }
            }
            else
            {
                if (NavMeshAgent.isOnNavMesh && NavMeshAgent.hasPath)
                {
                    float distance = NavMeshAgent.remainingDistance;

                    if (distance <= NavMeshAgent.stoppingDistance + NavMeshAgent.radius)
                    {
                        WaitingTimer = Random.Range(WaypointRoamTime.Value.x, WaypointRoamTime.Value.y);
                        IsWaiting = true;
                    }
                }
                else if (NavMeshAgent.isOnNavMesh)
                {
                    // Nếu chưa có lộ trình di chuyển, đi tới điểm tuần tra kế tiếp
                    MoveToNextWaypoint();
                }
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (NavMeshAgent != null && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.ResetPath();
            }
        }

        private void PickNavMeshWaypoints()
        {
            if (NavMeshAgent == null) return;

            NavMeshQueryFilter queryFilter = new()
                { agentTypeID = NavMeshAgent.agentTypeID, areaMask = NavMeshAgent.areaMask };
            NavMeshWaypoints = new List<Vector3>();
            int closestWaypointIndex = 0;
            int index = 0;
            float closestWaypointDistance = float.MaxValue;
            Vector3 currentPosition = NavMeshAgent.transform.position;

            float sampleRadius = Mathf.Max(NavMeshAgent.radius * 3.0f, 4.0f);

            if (Waypoints.Value != null && Waypoints.Value.Count > 0)
            {
                foreach (Vector3 waypoint in Waypoints.Value)
                {
                    if (NavMesh.SamplePosition(waypoint, out NavMeshHit hit, sampleRadius, queryFilter))
                    {
                        NavMeshWaypoints.Add(hit.position);
                        NavMeshPath path = new();
                        if (NavMesh.CalculatePath(currentPosition, hit.position, queryFilter, path) && path.status == NavMeshPathStatus.PathComplete)
                        {
                            float distance = NavMeshUtilities.GetSquareDistanceOfPath(path);

                            if (distance < closestWaypointDistance)
                            {
                                closestWaypointIndex = index;
                                closestWaypointDistance = distance;
                            }
                        }

                        index++;
                    }
                }
            }

            // Fallback an toàn: nếu không có điểm nào được sample, dùng vị trí hiện tại của agent để đảm bảo Count >= 1
            if (NavMeshWaypoints.Count == 0)
            {
                if (NavMesh.SamplePosition(currentPosition, out NavMeshHit currentHit, 5.0f, queryFilter))
                {
                    NavMeshWaypoints.Add(currentHit.position);
                }
                else
                {
                    NavMeshWaypoints.Add(currentPosition);
                }
            }

            CurrentPatrolPoint = Mathf.Clamp(closestWaypointIndex, 0, NavMeshWaypoints.Count - 1);
            CurrentTarget = NavMeshWaypoints[CurrentPatrolPoint];
            if (NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.SetDestination(CurrentTarget);
            }
        }

        private void MoveToNextWaypoint()
        {
            if (NavMeshWaypoints == null || NavMeshWaypoints.Count == 0)
            {
                PickNavMeshWaypoints();
                if (NavMeshWaypoints == null || NavMeshWaypoints.Count == 0) return;
            }

            CurrentPatrolPoint = (CurrentPatrolPoint + 1) % NavMeshWaypoints.Count;

            CurrentTarget = NavMeshWaypoints[CurrentPatrolPoint];
            if (NavMeshAgent != null && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.SetDestination(CurrentTarget);
            }
        }
    }
}
