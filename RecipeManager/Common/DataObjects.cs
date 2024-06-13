using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RecipeManager.Common
{
    internal class DataObjects
    {
        public enum Action
        {
            Disable,
            Delete,
            Modify,
            Add,
            Enable
        }

        public class TrackedRecipe
        {
            public Action action { get; set; }
            public string prefab { get; set; }
            public string recipeName { get; set; }
            public Recipe originalRecipe { get; set; }
            public Recipe updatedRecipe { get; set; }
            public CustomRecipe updatedCustomRecipe { get; set; }
            public CustomRecipe originalCustomRecipe { get; set; }
        }

        [DataContract]
        public class RecipeModificationCollection
        {
            public Dictionary<String,RecipeModification> RecipeModifications { get; set; } = new Dictionary<String,RecipeModification>();
        }

        [DataContract]
        public class RecipeModification
        {
            public Action action { get; set; }
            public string prefab { get; set; }
            public string recipeName { get; set; }
            public string craftedAt { get; set; }
            public short minStationLevel { get; set; } = 1;
            public short craftAmount { get; set; } = 1;
            public SimpleRecipe recipe { get; set; }
        }

        [DataContract]
        public class SimpleRecipe
        {
            public bool anyOneResource { get; set; } = false;
            public List<Ingrediant> ingredients { get; set; } = new List<Ingrediant>();
        }

        [DataContract]
        public class Ingrediant
        {
            public string prefab { get; set; }
            public short craftCost { get; set; }
            public short upgradeCost { get; set; }
            public bool refund { get; set; } = false;
        }
    }
}
