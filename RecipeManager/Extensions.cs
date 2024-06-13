using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecipeManager
{
    public static class Extensions
    {
        public static Recipe GetRecipe(this List<Recipe> list, Recipe recipe)
        {
            int index = ObjectDB.instance.m_recipes.IndexOf(recipe);
            if (index > -1)
            {
                return list[index];
            }

            string name = recipe.ToString();
            return ObjectDB.instance.m_recipes.FirstOrDefault(r => name.Equals(r.ToString()));
        }
    }
}
