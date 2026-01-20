// using UnityEngine;

// public class Shelf : MonoBehaviour, ITickable
// {
//     [SerializeField] private ItemCategory itemCategory;
//     [SerializeField] private float goldPerSec = 0;


//     private int cachedDisplay;  // number of items currently on shelf
//     private float goldTimer;

//     void OnEnable()
//     {
//         GameEvents.ProductCrafted += OnProductCrafted;
//     }
//     void OnDisable()
//     {
//         GameEvents.ProductCrafted -= OnProductCrafted;
//     }

//     private void OnProductCrafted(ResourceStack s)
//     {
//         if (s.id != productId) return;
//         TryRestockFromInventory();
//     }

//     private void TryRestockFromInventory()
//     {
//         while (cachedDisplay < displayCap && Inventory.Instance.Get(productId) > 0)
//         {
//             Inventory.Instance.TryRemove(productId, 1);
//             cachedDisplay += 1;
//         }
//     }

//     public void Tick(float dt)
//     {
//         if (cachedDisplay <= 0) { TryRestockFromInventory(); return; }
//         goldTimer += dt;
//         // Pay continuously (simple model)
//         // float goldToPay = goldPerSecondPerItem * cachedDisplay * dt;
//         int whole = Mathf.FloorToInt(goldToPay); // MVP: drop fractions or accumulate in a float buffer
//         if (whole > 0) Inventory.Instance.AddGold(whole);
//         // (You can keep a float accumulator to avoid losing fractional gold.)
//     }

//     void Update() => Tick(Time.deltaTime);
// }

// public static class CustomerCategory
// {
//     public static ItemCategory Preferred(CustomerType t)
//     {
//         switch (t)
//         {
//             case CustomerType.Farmer: return ItemCategory.Common;
//             case CustomerType.Guard:  return ItemCategory.Crafted;
//             default:                  return ItemCategory.Luxury; // Noble
//         }
//     }
// }
