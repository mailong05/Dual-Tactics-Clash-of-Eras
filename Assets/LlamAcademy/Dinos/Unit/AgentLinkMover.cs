using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using LlamAcademy.Dinos.Utility;
using Unity.AI.Navigation;

namespace LlamAcademy.Dinos.Unit
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
    public class AgentLinkMover : MonoBehaviour
    {
        private NavMeshAgent Agent;
        private Animator Animator;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Animator = GetComponent<Animator>();
        }

        private IEnumerator Start()
        {
            Agent.autoTraverseOffMeshLink = false;
            while (true)
            {
                if (!Agent.isOnOffMeshLink)
                {
                    yield return null;
                    continue;
                }

                OffMeshLinkData offMeshLinkData = Agent.currentOffMeshLinkData;
                Vector3 target = (Vector3.Distance(transform.position, offMeshLinkData.startPos) < Vector3.Distance(transform.position, offMeshLinkData.endPos))
                    ? offMeshLinkData.endPos
                    : offMeshLinkData.startPos;

                yield return StartCoroutine(MoveToTargetAtNormalSpeed(target + Vector3.up * Agent.baseOffset));

                Agent.CompleteOffMeshLink();
                yield return null;
            }
        }

        private IEnumerator MoveToTargetAtNormalSpeed(Vector3 target, float threshold = 0.35f)
        {
            float initialDist = Vector3.Distance(Agent.transform.position, target);
            float maxDuration = (initialDist / Mathf.Max(Agent.speed, 1.5f)) + 1.2f;
            float elapsed = 0f;

            while (Vector3.Distance(Agent.transform.position, target) > threshold && elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;
                Agent.transform.position = Vector3.MoveTowards(Agent.transform.position, target, Agent.speed * Time.deltaTime);

                Vector3 dir = (target - Agent.transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Agent.transform.rotation = Quaternion.Slerp(Agent.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
                }

                if (Animator != null)
                {
                    Animator.SetFloat(AnimationConstants.SPEED_PARAMETER, Agent.speed);
                }
                yield return null;
            }

            if (Vector3.Distance(Agent.transform.position, target) <= 1.2f)
            {
                Agent.transform.position = target;
            }
        }
    }
}
