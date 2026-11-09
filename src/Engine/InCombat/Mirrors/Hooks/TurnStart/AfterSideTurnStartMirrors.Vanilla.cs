using CombatSolver.Engine.Common.Mirrors;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;

internal static partial class AfterSideTurnStartMirrors
{
    private static partial void RegisterVanilla(MethodMirrorRegistry<AbstractModel, AfterSideTurnStartMirrorContext> registry)
    {
        registry.Register<BiasedCognitionPower>(static (_, _) => { });
        registry.Register<BlurPower>(static (_, _) => { });
        registry.Register<ClarityPower>(static (_, _) => { });
        registry.Register<CoolantPower>(static (_, _) => { });
        registry.Register<CountdownPower>(static (_, _) => { });
        registry.Register<DemonFormPower>(static (_, _) => { });
        registry.Register<DrawCardsNextTurnPower>(static (_, _) => { });
        registry.Register<FeralPower>(static (_, _) => { });
        registry.Register<FurnacePower>(static (_, _) => { });
        registry.Register<NeurosurgePower>(static (_, _) => { });
        registry.Register<NoxiousFumesPower>(static (_, _) => { });
        registry.Register<PlatingPower>(static (_, _) => { });
        registry.Register<PoisonPower>(static (_, _) => { });
        registry.Register<PrepTimePower>(static (_, _) => { });
        registry.Register<RampartPower>(static (_, _) => { });
        registry.Register<ReflectPower>(static (_, _) => { });
        registry.Register<ShadowStepPower>(static (_, _) => { });
        registry.Register<SlowPower>(static (_, _) => { });
        registry.Register<WraithFormPower>(static (_, _) => { });

        registry.Register<Akabeko>(static (_, _) => { });
        registry.Register<BigHat>(static (_, _) => { });
        registry.Register<BoomingConch>(static (_, _) => { });
        registry.Register<Bread>(static (_, _) => { });
        registry.Register<Brimstone>(static (_, _) => { });
        registry.Register<Candelabra>(static (_, _) => { });
        registry.Register<Chandelier>(static (_, _) => { });
        registry.Register<Crossbow>(static (_, _) => { });
        registry.Register<DiamondDiadem>(static (_, _) => { });
        registry.Register<DivineDestiny>(static (_, _) => { });
        registry.Register<FakeHappyFlower>(static (_, _) => { });
        registry.Register<FencingManual>(static (_, _) => { });
        registry.Register<HappyFlower>(static (_, _) => { });
        registry.Register<InfusedCore>(static (_, _) => { });
        registry.Register<Lantern>(static (_, _) => { });
        registry.Register<LetterOpener>(static (_, _) => { });
        registry.Register<OrangeDough>(static (_, _) => { });
        registry.Register<PaelsEye>(static (_, _) => { });
        registry.RegisterIgnored<PaelsFlesh>();
        registry.Register<PaelsLegion>(static (_, _) => { });
        registry.Register<PaelsTears>(static (_, _) => { });
        registry.Register<PhylacteryUnbound>(static (_, _) => { });
        registry.RegisterIgnored<Pocketwatch>();
        registry.Register<RunicCapacitor>(static (_, _) => { });
        registry.Register<Sai>(static (_, _) => { });
        registry.Register<SealOfGold>(static (_, _) => { });
        registry.RegisterIgnored<StoneCalendar>();
        registry.Register<SymbioticVirus>(static (_, _) => { });
        registry.Register<VeryHotCocoa>(static (_, _) => { });
    }
}
