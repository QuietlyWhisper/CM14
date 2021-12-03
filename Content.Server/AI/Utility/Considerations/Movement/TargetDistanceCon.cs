using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.Utility.Considerations.Movement
{
    public sealed class TargetDistanceCon : Consideration
    {
        protected override float GetScore(Blackboard context)
        {
            var self = context.GetState<SelfState>().GetValue();
            var target = context.GetState<TargetEntityState>().GetValue();
            if (target == null || (!IoCManager.Resolve<IEntityManager>().EntityExists(target.Uid) ? EntityLifeStage.Deleted : IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(target.Uid).EntityLifeStage) >= EntityLifeStage.Deleted || target.Transform.GridID != self?.Transform.GridID)
            {
                return 0.0f;
            }

            // Anything further than 100 tiles gets clamped
            return (target.Transform.Coordinates.Position - self.Transform.Coordinates.Position).Length / 100;
        }
    }
}
