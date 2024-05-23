using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.Configuration;
public interface IGlobalConfiguration
{
    RootConfiguration Get();
}
