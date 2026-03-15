using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace HVPAA
{
    //for when a psycast's effective use requires nearby same-faction pawns to bunch up on the caster. E.g. Farskip to gedda hell outta dere, or Neuroquake to avoid being hit by the shockwave
    public class JobDriver_FollowRally : JobDriver
    {
        private Pawn Followee
        {
            get
            {
                return (Pawn)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }
        private bool CurrentlyWalkingToFollowee
        {
            get
            {
                return this.pawn.pather.Moving && this.pawn.pather.Destination == this.Followee;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }
        public override void Notify_Starting()
        {
            base.Notify_Starting();
            this.job.followRadius = 3f;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.tickAction = delegate
            {
                Pawn followee = this.Followee;
                float followRadius = this.job.followRadius;
                if (!this.pawn.pather.Moving || this.pawn.IsHashIntervalTick(30))
                {
                    bool flag = false;
                    if (this.CurrentlyWalkingToFollowee)
                    {
                        if (JobDriver_FollowRally.NearFollowee(this.pawn, followee, followRadius))
                        {
                            flag = true;
                        }
                    }
                    else
                    {
                        float num = followRadius * 1.2f;
                        if (JobDriver_FollowRally.NearFollowee(this.pawn, followee, num))
                        {
                            flag = true;
                        }
                        else
                        {
                            if (!this.pawn.CanReach(followee, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn))
                            {
                                base.EndJobWith(JobCondition.Incompletable);
                                return;
                            }
                            this.pawn.pather.StartPath(followee, PathEndMode.Touch);
                            this.locomotionUrgencySameAs = null;
                        }
                    }
                    if (Find.TickManager.TicksGame - this.startTick > this.maxRallyTicks)
                    {
                        base.EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    if (flag)
                    {
                        if (JobDriver_FollowRally.NearDestinationOrNotMoving(this.pawn, followee, followRadius))
                        {
                            this.pawn.pather.StartPath(followee, PathEndMode.Touch);
                            return;
                        }
                        IntVec3 lastPassableCellInPath = followee.pather.LastPassableCellInPath;
                        if (!this.pawn.pather.Moving || this.pawn.pather.Destination.HasThing || !this.pawn.pather.Destination.Cell.InHorDistOf(lastPassableCellInPath, followRadius))
                        {
                            IntVec3 intVec = CellFinder.RandomClosewalkCellNear(lastPassableCellInPath, base.Map, UnityEngine.Mathf.FloorToInt(followRadius), null);
                            if (intVec.IsValid && this.pawn.CanReach(intVec, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
                            {
                                this.pawn.pather.StartPath(intVec, PathEndMode.OnCell);
                                this.locomotionUrgencySameAs = followee;
                                return;
                            }
                            return;
                        }
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
            yield break;
        }
        public override bool IsContinuation(Job j)
        {
            return this.job.GetTarget(TargetIndex.A) == j.GetTarget(TargetIndex.A);
        }
        public static bool FarEnoughAndPossibleToStartJob(Pawn follower, Pawn followee, float radius)
        {
            if (radius <= 0f)
            {
                string text = "Checking follow job with radius <= 0. pawn=" + follower.ToStringSafe<Pawn>();
                if (follower.mindState != null && follower.mindState.duty != null)
                {
                    text = text + " duty=" + follower.mindState.duty.def;
                }
                Log.ErrorOnce(text, follower.thingIDNumber ^ 843254009);
                return false;
            }
            if (!follower.CanReach(followee, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
            {
                return false;
            }
            float num = radius * 1.2f;
            return !JobDriver_FollowRally.NearFollowee(follower, followee, num) || (!JobDriver_FollowRally.NearDestinationOrNotMoving(follower, followee, num) && follower.CanReach(followee.pather.LastPassableCellInPath, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn));
        }
        private static bool NearFollowee(Pawn follower, Pawn followee, float radius)
        {
            return follower.Position.AdjacentTo8WayOrInside(followee.Position) || (follower.Position.InHorDistOf(followee.Position, radius));
        }
        private static bool NearDestinationOrNotMoving(Pawn follower, Pawn followee, float radius)
        {
            if (!followee.pather.Moving)
            {
                return true;
            }
            IntVec3 lastPassableCellInPath = followee.pather.LastPassableCellInPath;
            return !lastPassableCellInPath.IsValid || follower.Position.AdjacentTo8WayOrInside(lastPassableCellInPath) || follower.Position.InHorDistOf(lastPassableCellInPath, radius);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxRallyTicks, "maxRallyTicks", 750);
        }
        private const TargetIndex FolloweeInd = TargetIndex.A;
        private const int CheckPathIntervalTicks = 30;
        public int maxRallyTicks = 750;
    }
}
