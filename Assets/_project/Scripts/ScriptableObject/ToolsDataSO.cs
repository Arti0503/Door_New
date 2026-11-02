using System.Collections.Generic;
using CCEPL_CAD.Models;
using UnityEngine;

namespace CCEPL_CAD
{
    [CreateAssetMenu(menuName = "Simulation/Tools Data")]
    public class ToolsDataSO : ScriptableObject
    {
        public List<ToolsData> ToolsData;
    }
}