using CombatSolver.Engine.Common.Mirrors;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Powers.Mocks;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnEnd;

internal static partial class AfterSideTurnEndMirrors
{
    private static partial void RegisterVanilla(MethodMirrorRegistry<AbstractModel, AfterSideTurnEndMirrorContext> registry)
    {
        registry.Register<AsleepPower>(static (_, _) => { });
        registry.Register<BattlewornDummyTimeLimitPower>(static (_, _) => { });
        registry.Register<BorrowedTimePower>(static (_, _) => { });
        registry.Register<BurstPower>(static (_, _) => { });
        registry.Register<ColossusPower>(static (_, _) => { });
        registry.Register<ConcoctPower>(static (_, _) => { });
        registry.Register<ConquerorPower>(static (_, _) => { });
        registry.Register<ConstrictPower>(static (_, _) => { });
        registry.Register<ConsumingShadowPower>(static (_, _) => { });
        registry.Register<CorrosiveWavePower>(static (_, _) => { });
        registry.RegisterIgnored<CoveredPower>();
        registry.Register<DarkEmbracePower>(static (_, _) => { });
        registry.Register<DebilitatePower>(static (_, _) => { });
        registry.Register<DemisePower>(static (_, _) => { });
        registry.Register<DoomPower>(static (_, _) => { });
        registry.Register<DoubleDamagePower>(static (_, _) => { });
        registry.Register<DuplicationPower>(static (_, _) => { });
        registry.Register<EscapeArtistPower>(static (_, _) => { });
        registry.Register<FlameBarrierPower>(static (_, _) => { });
        registry.RegisterIgnored<FlankingPower>();
        registry.Register<FrailPower>(static (_, _) => { });
        registry.Register<GravityPower>(static (_, _) => { });
        registry.Register<HatchPower>(static (_, _) => { });
        registry.Register<HellraiserPower>(static (_, _) => { });
        registry.Register<HighVoltagePower>(static (_, _) => { });
        registry.Register<IntangiblePower>(static (_, _) => { });
        registry.RegisterIgnored<InterceptPower>();
        registry.Register<JugglingPower>(static (_, _) => { });
        registry.RegisterIgnored<KnockdownPower>();
        registry.Register<MagicBombPower>(static (_, _) => { });
        registry.RegisterIgnored<MockPhaseObserverPower>();
        registry.Register<MonologuePower>(static (_, _) => { });
        registry.Register<NemesisPower>(static (_, _) => { });
        registry.Register<NoBlockPower>(static (_, _) => { });
        registry.Register<NoDrawPower>(static (_, _) => { });
        registry.Register<NoEnergyGainPower>(static (_, _) => { });
        registry.Register<OblivionPower>(static (_, _) => { });
        registry.Register<OneTwoPunchPower>(static (_, _) => { });
        registry.Register<PaleBlueDotPower>(static (_, _) => { });
        registry.Register<PanachePower>(static (_, _) => { });
        registry.Register<RagePower>(static (_, _) => { });
        registry.Register<ReboundPower>(static (_, _) => { });
        registry.Register<RetainHandPower>(static (_, _) => { });
        registry.Register<RingingPower>(static (_, _) => { });
        registry.Register<RitualPower>(static (_, _) => { });
        registry.Register<ShadowmeldPower>(static (_, _) => { });
        registry.Register<ShrinkPower>(static (_, _) => { });
        registry.Register<SicEmPower>(static (_, _) => { });
        registry.Register<SkittishPower>(static (_, _) => { });
        registry.Register<SlumberPower>(static (_, _) => { });
        registry.Register<SmoggyPower>(static (_, _) => { });
        registry.Register<StranglePower>(static (_, _) => { });
        registry.Register<TaintedPower>(static (_, _) => { });
        registry.Register<TangledPower>(static (_, _) => { });
        registry.Register<TenderPower>(static (_, _) => { });
        registry.Register<TerritorialPower>(static (_, _) => { });
        registry.Register<UnderworldPower>(static (_, _) => { });
        registry.Register<VulnerablePower>(static (_, _) => { });
        registry.Register<WeakPower>(static (_, _) => { });

        registry.Register<ArtOfWar>(static (_, _) => { });
        registry.Register<JossPaper>(static (_, _) => { });
        registry.Register<Kusarigama>(static (_, _) => { });
        registry.Register<LunarPastry>(static (_, _) => { });
        registry.Register<ParryingShield>(static (_, _) => { });
    }
}
