namespace Myria.ConsoleApp.Commands
{
    public static class InventoryCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input.StartsWith("equip "))
            {
                EquipItem(input.Substring(6).Trim(), Character);
                return true;
            }
            else if (input.StartsWith("unequip "))
            {
                Unequip(Character, input.Substring(8).Trim().ToLower());
                return true;
            }
            else if (input.StartsWith("use "))
            {
                UseItem(input.Substring(4).Trim(), Character);
                return true;
            }
            else if (input.StartsWith("inventory"))
            {
                Character.Inventory.ListItems();
                Console.WriteLine(Localization.T("console.inventory.money", Character.Money));
                return true;
            }
            else if (input.StartsWith("search "))
            {
                SearchInventory(Character, input.Substring(7).Trim());
                return true;
            }
            else if (input.StartsWith("show "))
            {
                ShowItem(Character, input.Substring(5).Trim());
                return true;
            }

            return false;
        }

        private static void EquipItem(string itemName, Character Character)
        {
            var match = InventoryUtils.ResolveInventoryItem(itemName, Character);
            if (match is not EquipmentItem equipment)
            {
                Console.WriteLine(Localization.T("console.inventory.cannot_equip"));
                return;
            }
            if (!equipment.IsUsableBy(Character))
            {
                Console.WriteLine(Localization.T("console.inventory.class_cannot_equip"));
                return;
            }

            if (ConsoleHubClient.IsConnected)
            {
                var result = ConsoleHubClient.EquipItemAsync(equipment.Id).GetAwaiter().GetResult();
                if (result == null || !result.Success)
                {
                    Printer.PrintError(result?.Reason ?? Localization.T("console.inventory.equip_failed"));
                    return;
                }
            }

            Character.Equip(equipment);
            Character.Inventory.RemoveItem(equipment);
        }

        private static void Unequip(Character Character, string slot)
        {
            EquipmentItem? item = slot switch
            {
                "weapon"    => Character.WeaponSlot,
                "armor"     => Character.ArmorSlot,
                "accessory" => Character.AccessorySlot,
                _           => null
            };

            if (item == null) { Printer.PrintError(Localization.T("console.inventory.no_item_in_slot", slot)); return; }

            if (ConsoleHubClient.IsConnected)
            {
                string slotType = slot switch { "weapon" => "Weapon", "armor" => "Armor", "accessory" => "Accessory", _ => slot };
                bool ok = ConsoleHubClient.UnequipItemAsync(slotType).GetAwaiter().GetResult();
                if (!ok) { Printer.PrintError(Localization.T("console.inventory.unequip_failed")); return; }
            }

            if (Character.Inventory.AddItem(item, Character))
            {
                switch (slot)
                {
                    case "weapon":    Character.WeaponSlot    = null; break;
                    case "armor":     Character.ArmorSlot     = null; break;
                    case "accessory": Character.AccessorySlot = null; break;
                }
                Printer.PrintSuccess(Localization.T("console.inventory.unequipped", Localization.T(item.Name)));
            }
            else
            {
                Printer.PrintError(Localization.T("console.inventory.unequip_inventory_full"));
            }
        }

        private static void UseItem(string itemName, Character Character)
        {
            var item = InventoryUtils.ResolveInventoryItem(itemName, Character);
            if (item == null) { Console.WriteLine(Localization.T("console.inventory.dont_have", itemName)); return; }

            if (item is ConsumableItem consumable)
            {
                if (ConsoleHubClient.IsConnected)
                {
                    bool ok = ConsoleHubClient.UseItemAsync(item.Id).GetAwaiter().GetResult();
                    if (!ok) { Printer.PrintError(Localization.T("console.inventory.use_failed")); return; }
                }

                consumable.Use(Character);
                Character.Inventory.ConsumeOne(item);
            }
            else
            {
                Console.WriteLine(Localization.T("console.inventory.cannot_use", Localization.T(item.Name)));
            }
        }

        private static void ShowItem(Character Character, string itemName)
        {
            var item = InventoryUtils.ResolveInventoryItem(itemName, Character)
                ?? (Character.WeaponSlot?.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) == true ? Character.WeaponSlot : null)
                ?? (Character.ArmorSlot?.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) == true ? Character.ArmorSlot : null)
                ?? (Character.AccessorySlot?.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) == true ? Character.AccessorySlot : null);

            if (item == null) { Printer.PrintError(Localization.T("console.inventory.no_item_named", itemName)); return; }

            Console.WriteLine(Localization.T("console.inventory.item_name", Localization.T(item.Name)));
            Console.WriteLine(Localization.T("console.inventory.item_description", Localization.T(item.Description)));

            if (item is EquipmentItem eq)
            {
                Console.WriteLine(Localization.T("console.inventory.bonuses_header"));
                PrintBonus("ATK",     eq.BonusATK);
                PrintBonus("DEF",     eq.BonusDEF);
                PrintBonus("MATK",    eq.BonusMATK);
                PrintBonus("MDEF",    eq.BonusMDEF);
                PrintBonus("Aim",     eq.BonusAim);
                PrintBonus("Evasion", eq.BonusEvasion);
                PrintBonus("STR",     eq.BonusSTR);
                PrintBonus("DEX",     eq.BonusDEX);
                PrintBonus("END",     eq.BonusEND);
                PrintBonus("INT",     eq.BonusINT);
                PrintBonus("SPR",     eq.BonusSPR);
                PrintBonus("Crit",    eq.BonusCrit);
                PrintBonus("Block",   eq.BonusBlock);

                if (eq.AllowedClasses.Any())
                    Console.WriteLine(Localization.T("console.inventory.usable_by", string.Join(", ", eq.AllowedClasses)));
            }
        }

        private static void SearchInventory(Character Character, string term)
        {
            var items = Character.Inventory.Items;
            IEnumerable<Item> result = term.ToLower() switch
            {
                "usable"    => items.Where(i => i is ConsumableItem),
                "equip"     => items.Where(i => i is EquipmentItem),
                "material"  => items.Where(i => i is MaterialItem),
                "weapon"    => items.OfType<EquipmentItem>().Where(e => e.SlotType == EquipmentType.Weapon),
                "armor"     => items.OfType<EquipmentItem>().Where(e => e.SlotType == EquipmentType.Armor),
                "accessory" => items.OfType<EquipmentItem>().Where(e => e.SlotType == EquipmentType.Accessory),
                _           => items.Where(i => i.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            };

            if (!result.Any()) { Console.WriteLine(Localization.T("console.inventory.search_no_matches")); return; }

            Console.WriteLine(Localization.T("console.inventory.search_header", term));
            foreach (var item in result)
                Console.WriteLine(Localization.T("console.common.list_item", Localization.T(item.Name)));
        }

        private static void PrintBonus(string name, int value)
        {
            if (value != 0) Console.WriteLine(Localization.T("console.inventory.bonus_line", value, name));
        }

        private static void PrintBonus(string name, float value)
        {
            if (value != 0) Console.WriteLine(Localization.T("console.inventory.bonus_pct_line", value, name));
        }

    }

}
