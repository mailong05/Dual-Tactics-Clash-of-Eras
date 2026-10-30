using LlamAcademy.Dinos.AI;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class RunnerDino : PrehistoricDinoBase
    {
        protected override void Awake()
        {
            AISpeciesType = DinoAISpeciesType.VelociraptorFlanker;
            MoveSpeed = 8.5f;
            base.Awake();
            ConfigureNavMeshAgent();
            if (Agent != null)
            {
                Agent.speed = 8.5f;
                Agent.acceleration = 36f;
                Agent.angularSpeed = 480f;
            }
            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 450;
            Health = MaxHealth;
            AttackDamage = 22;
            AttackInterval = 0.5f;
            BuildingDamageMultiplier = 2.5f;
            GoldReward = 12;

            if (VisualModel != null)
            {
                // Nâng mô hình velociraptor.glb lên +12.987m để triệt tiêu độ lệch trục -1.1m từ Blender (10.9x scale),
                // đưa toàn bộ 100% thân, chân và mắt khủng long trồi lên mặt đất chính xác, tuyệt đối không bị chìm dưới lòng đất!
                VisualModel.localPosition = new Vector3(0f, 12.987f, 0f);
                VisualModel.localRotation = Quaternion.identity; // Căn chuẩn 100% đầu và thân hướng thẳng phía trước về phía căn cứ đối phương
                _BaseModelLocalPos = VisualModel.localPosition;
                _BaseModelLocalRot = Quaternion.identity;

                // Mở rộng localBounds của toàn bộ SkinnedMeshRenderer để Unity URP không bao giờ cull/tàng hình
                foreach (var smr in VisualModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;
                    smr.allowOcclusionWhenDynamic = false;
                    smr.localBounds = new Bounds(new Vector3(0f, -1.1f, 0f), new Vector3(5f, 5f, 5f));
                }

                // Xóa bỏ hoàn toàn Animator trên Model để không bao giờ xung đột hay đè xương
                if (ModelAnimator != null)
                {
                    ModelAnimator.runtimeAnimatorController = null;
                    ModelAnimator.enabled = false;
                    Destroy(ModelAnimator);
                    ModelAnimator = null;
                }

                // Cài đặt trực tiếp Animation (Legacy) từ file GLB của người dùng:
                // Tìm đúng host "Sketchfab_model" - nơi khớp 100% đường dẫn xương "root/GLTF_SceneRootNode/..."
                Transform animHost = VisualModel.Find("Sketchfab_model");
                if (animHost == null) animHost = VisualModel;

                Animation anim = animHost.GetComponent<Animation>();
                if (anim == null) anim = animHost.gameObject.AddComponent<Animation>();

                AnimationClip walkClip = Resources.Load<AnimationClip>("Animations/Velociraptor_Walk");
                AnimationClip runClip = Resources.Load<AnimationClip>("Animations/Velociraptor_Run");
                AnimationClip attackClip = Resources.Load<AnimationClip>("Animations/Velociraptor_Attack");

                if (walkClip != null)
                {
                    walkClip.legacy = true;
                    walkClip.wrapMode = WrapMode.Loop;
                    if (anim["Walk"] == null) anim.AddClip(walkClip, "Walk");
                    if (anim["Velociraptor_Walk"] == null) anim.AddClip(walkClip, "Velociraptor_Walk");
                    anim.clip = walkClip;
                    _WalkClipName = "Walk";
                    _IdleClipName = "Walk";
                }

                if (runClip != null)
                {
                    runClip.legacy = true;
                    runClip.wrapMode = WrapMode.Loop;
                    if (anim["Run"] == null) anim.AddClip(runClip, "Run");
                    if (anim["Velociraptor_Run"] == null) anim.AddClip(runClip, "Velociraptor_Run");
                }

                if (attackClip != null)
                {
                    attackClip.legacy = true;
                    attackClip.wrapMode = WrapMode.Once;
                    if (anim["Attack"] == null) anim.AddClip(attackClip, "Attack");
                    if (anim["Velociraptor_Attack"] == null) anim.AddClip(attackClip, "Velociraptor_Attack");
                    _AttackClipName = "Attack";
                }

                anim.playAutomatically = true;
                anim.enabled = true;
                if (!string.IsNullOrEmpty(_WalkClipName)) anim.Play(_WalkClipName);

                ModelLegacyAnimation = anim;
            }
        }
    }
}
