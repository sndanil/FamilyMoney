using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.State;

public interface IStateManager
{
    MainState GetMainState();

    void SetMainState(MainState state);
}
