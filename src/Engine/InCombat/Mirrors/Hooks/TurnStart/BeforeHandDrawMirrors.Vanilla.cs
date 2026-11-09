using CombatSolver.Engine.Common.Mirrors;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Powers.Mocks;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;

internal static partial class BeforeHandDrawMirrors
{
    private static partial void RegisterVanilla(MethodMirrorRegistry<AbstractModel, BeforeHandDrawMirrorContext> registry)
    {
        registry.Register<Bolas>(static (_, _) => { });
        registry.Register<ThrummingHatchet>(static (_, _) => { });

        registry.Register<CallOfTheVoidPower>(static (_, _) => { });
        registry.Register<CreativeAiPower>(static (_, _) => { });
        registry.Register<ForegoneConclusionPower>(static (_, _) => { });
        registry.Register<HelloWorldPower>(static (_, _) => { });
        registry.Register<InfiniteBladesPower>(static (_, _) => { });
        registry.Register<NightmarePower>(static (_, _) => { });
        registry.Register<SentryModePower>(static (_, _) => { });
        registry.Register<SpectrumShiftPower>(static (_, _) => { });
        registry.RegisterIgnored<MockPhaseObserverPower>();

        registry.Register<BlessedAntler>(static (_, _) => { });
        registry.Register<FuneraryMask>(static (_, _) => { });
        registry.Register<JeweledMask>(static (_, _) => { });
        registry.Register<NinjaScroll>(static (_, _) => { });
        registry.Register<Pendulum>(static (_, _) => { });
        registry.Register<PollinousCore>(static (_, _) => { });
        registry.Register<RadiantPearl>(static (_, _) => { });
        registry.Register<Toolbox>(static (_, _) => { });
    }
}
