// ── Myria.Lib.Core entity namespaces ─────────────────────────────────────────────
global using Myria.Lib.Core.Entities;
global using Myria.Lib.Core.Entities.Characters;
global using Myria.Lib.Core.Entities.Monsters;
global using Myria.Lib.Core.Entities.Maps;
global using Myria.Lib.Core.Entities.Items;
global using Myria.Lib.Core.Entities.Skills;
global using Myria.Lib.Core.Entities.NPCs;

// ── Myria.Lib.Core model / enum / interface / util namespaces ───────────────────
global using Myria.Lib.Core.Systems.Enums;
global using Myria.Lib.Core.Systems.Interfaces;
global using Myria.Lib.Core.Models;
global using Myria.Lib.Core.Models.Dto;
global using Myria.Lib.Core.Models.Settings;
global using Myria.Lib.Core.Utils;

// ── Myria.Lib.Core service namespaces that have no name conflicts ───────────────
global using Myria.Lib.Core.Services.Builder;     // ItemFactory, SkillFactory, MapBuilder
global using Myria.Lib.Core.Services.Regestries;  // DungeonRegistry, CaveRegistry, CityRegistry, ForestRegistry

// ── Myria.Lib.Core types that would conflict if their namespace was imported whole ─
// Myria.Lib.Core.Services: GameService conflicts with Myria.ConsoleApp.Services.GameService
// Myria.Lib.Core.Services.Manager: LoginManager, DayCycleManager  — LoginManager conflicts
global using CharacterService = Myria.Lib.Core.Services.CharacterService;
global using NpcService = Myria.Lib.Core.Services.NpcService;
global using RoomService = Myria.Lib.Core.Services.RoomService;
global using SettingsService = Myria.Lib.Core.Services.SettingsService;
global using QuestManager = Myria.Lib.Core.Services.Manager.QuestManager;
global using JobManager = Myria.Lib.Core.Services.Manager.JobManager;
global using ClassManager = Myria.Lib.Core.Services.Manager.ClassManager;
global using StartingEquipmentService = Myria.Lib.Core.Services.Manager.StartingEquipmentService;
global using JobXpService = Myria.Lib.Core.Services.JobXpService;
global using DayCycleManager = Myria.Lib.Core.Services.Manager.DayCycleManager;
global using LootGenerator = Myria.Lib.Core.Systems.LootGenerator;
global using SkillFusionSystem = Myria.Lib.Core.Systems.SkillFusionSystem;
global using SkillCombinationService = Myria.Lib.Core.Services.SkillCombinationService;
global using SkillSlotService = Myria.Lib.Core.Services.SkillSlotService;
global using GameLog = Myria.Lib.Core.Systems.GameLog;
global using GameStatus = Myria.Lib.Core.Systems.GameStatus;
global using ItemConverter = Myria.Lib.Core.Systems.ItemConverter;
// Console used to have its own near-duplicate Localization class (Systems/Localisation.cs) —
// retired in favor of the shared one, which now supports per-project locale additions via
// Localization.RegisterAdditionalLocaleDirectory (see Services/GameService.cs).
global using Localization = Myria.Lib.Core.Systems.Localization;

// ── Myria.ConsoleApp own types ─────────────────────────────────────────────
global using Myria.ConsoleApp.Commands;       // SkillCommands, CharacterCommands, etc.
global using Myria.ConsoleApp.Services;       // GameService, LoginManager (console wrappers)
global using Myria.ConsoleApp.Systems;        // EncounterRunner, CommandRouter
global using Myria.ConsoleApp.Utils;          // Printer
global using Myria.ConsoleApp.Entities.NPCs;  // NpcInteractionHandler
