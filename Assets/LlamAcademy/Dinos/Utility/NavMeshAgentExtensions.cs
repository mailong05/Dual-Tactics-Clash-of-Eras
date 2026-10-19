using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Utility
{
    public static class NavMeshAgentExtensions
    {
        public static bool SetDestinationImmediate(
            this NavMeshAgent agent,
            Vector3 targetLocation,
            float positionLeniency = 0)
        {
            if (agent == null || !agent.isActiveAndEnabled)
            {
                return false;
            }

            NavMeshQueryFilter queryFilter = new() {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            // Nếu agent chưa ở trên NavMesh (ví dụ vừa spawn, đang rơi hoặc bị hẫng chân):
            // Thử đưa agent về điểm NavMesh hợp lệ gần nhất
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit agentHit, 4.0f, queryFilter))
                {
                    agent.Warp(agentHit.position);
                }

                if (!agent.isOnNavMesh)
                {
                    return false;
                }
            }

            NavMeshPath path = new();
            if (positionLeniency != 0)
            {
                if (!NavMesh.SamplePosition(targetLocation, out NavMeshHit hit, positionLeniency, queryFilter))
                {
                    return false;
                }

                targetLocation = hit.position;
            }

            bool canSetPath = NavMesh.CalculatePath(
                agent.transform.position,
                targetLocation,
                queryFilter,
                path
            );

            if (canSetPath && path.status != NavMeshPathStatus.PathInvalid && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                try
                {
                    agent.SetPath(path);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[NavMeshAgentExtensions] Failed to SetPath: {ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}
