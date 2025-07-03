using System.Collections.Generic;
using UnityEngine;

namespace AiEditor
{
    public enum AiBranchType { None, Turret, Nav }

    [CreateAssetMenu(fileName = "AiTreeAsset", menuName = "AI/Tree Asset", order = 1)]
    public class AiTreeAsset : ComponentData
    {
        [Header("AI Tree Configuration")]
        public AiBranchType branchType;
        public List<AiNodeData> nodes = new List<AiNodeData>();
        public List<AiConnectionData> connections = new List<AiConnectionData>();
        
        [Header("Execution Data")]
        public List<AiExecutableNode> executableNodes = new List<AiExecutableNode>();
        public string startNodeId;
        
        // Legacy field for compatibility - should be kept in sync with title
        [SerializeField]
        private string treeName;
        
        /// <summary>
        /// Gets or sets the tree name, ensuring synchronization with the title property
        /// </summary>
        public string TreeName
        {
            get { return string.IsNullOrEmpty(title) ? treeName : title; }
            set 
            { 
                title = value;
                treeName = value;
            }
        }
        
        /// <summary>
        /// Initialize the component category and default values
        /// </summary>
        private void OnEnable()
        {
            // Set category to AITree for unified handling
            category = ComponentCategory.AITree;
            
            // Synchronize title and treeName for legacy compatibility
            if (!string.IsNullOrEmpty(treeName) && string.IsNullOrEmpty(title))
            {
                title = treeName;
            }
            else if (!string.IsNullOrEmpty(title) && string.IsNullOrEmpty(treeName))
            {
                treeName = title;
            }
            
            // Set default values if not set
            if (cost == 0) cost = 100;
            if (weight == 0) weight = 1;
            
            // Ensure instanceId is set for legacy assets that might not have one
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = System.Guid.NewGuid().ToString();
            }
            
            // Initialize description if empty
            if (string.IsNullOrEmpty(description))
            {
                description = $"AI behavior tree for {branchType} control";
            }
        }
    }

    [System.Serializable]
    public class AiNodeData
    {
        public string nodeId; // Unique per node
        public string nodeType; // e.g. "Action", "Condition", "Wander"
        public string nodeLabel;
        public Vector2 position;
        public Dictionary<string, string> properties = new Dictionary<string, string>();
    }    [System.Serializable]
    public class AiConnectionData
    {
        public string fromNodeId;
        public string fromPortId; // Optional, for multi-port support
        public string toNodeId;
        public string toPortId;   // Optional, for multi-port support
    }

    [System.Serializable]
    public class AiExecutableNode
    {
        public string nodeId;
        public string methodName;  // e.g., "IfSelf", "IfRifle", "Fire", "Wander"
        public string originalLabel; // Original node label for reference
        public AiNodeType nodeType;
        public float numericValue; // For nodes with numbers (e.g., "If HP > 50%" -> 50.0f)
        public List<string> connectedNodeIds = new List<string>(); // For execution path
        public Vector2 position; // Y-position for priority sorting
    }

    [System.Serializable]
    public enum AiNodeType
    {
        Start,
        Condition,
        Action,
        SubAI
    }
}
