using Sandbox.Common.ObjectBuilders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;

namespace RealisticTurretAI
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeGatlingTurret), true)]
    public class RealisticLargeGatlingTurret : RealisticTurretAIGameLogicComponent { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeMissileTurret), true)]
    public class RealisticLargeMissileTurret : RealisticTurretAIGameLogicComponent { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorTurret), true)]
    public class RealisticInteriorTurret : RealisticTurretAIGameLogicComponent { }

    public class RealisticTurretAIGameLogicComponent : MyGameLogicComponent
    {
    }
}
