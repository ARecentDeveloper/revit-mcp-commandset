﻿using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class CreateLineElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;
        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        /// <summary>
        /// Creation data (input data)
        /// </summary>
        public List<LineElement> CreatedInfo { get; private set; }
        /// <summary>
        /// Execution result (output data)
        /// </summary>
        public AIResult<List<int>> Result { get; private set; }

        public string _wallName = "Generic - ";
        public string _ductName = "Rectangular Duct - ";

        /// <summary>
        /// Set creation parameters
        /// </summary>
        public void SetParameters(List<LineElement> data)
        {
            CreatedInfo = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementIds = new List<int>();
                foreach (var data in CreatedInfo)
                {
                    // Step0 Get component type
                    BuiltInCategory builtInCategory = BuiltInCategory.INVALID;
                    Enum.TryParse(data.Category.Replace(".", ""), true, out builtInCategory);

                    // Step1 Get level and offset
                    Level baseLevel = null;
                    Level topLevel = null;
                    double topOffset = -1;  // ft
                    double baseOffset = -1; // ft
                    baseLevel = doc.FindNearestLevel(data.BaseLevel / 304.8);
                    baseOffset = (data.BaseOffset + data.BaseLevel) / 304.8 - baseLevel.Elevation;
                    topLevel = doc.FindNearestLevel((data.BaseLevel + data.BaseOffset + data.Height) / 304.8);
                    topOffset = (data.BaseLevel + data.BaseOffset + data.Height) / 304.8 - topLevel.Elevation;
                    if (baseLevel == null)
                        continue;

                    // Step2 Get family type
                    FamilySymbol symbol = null;
                    WallType wallType = null;
                    DuctType ductType = null;

                    if (data.TypeId != -1 && data.TypeId != 0)
                    {
                        ElementId typeELeId = new ElementId(data.TypeId);
                        if (typeELeId != null)
                        {
                            Element typeEle = doc.GetElement(typeELeId);
                            if (typeEle != null && typeEle is FamilySymbol)
                            {
                                symbol = typeEle as FamilySymbol;
                                // Get symbol's Category object and convert to BuiltInCategory enum
                                builtInCategory = (BuiltInCategory)symbol.Category.Id.IntegerValue;
                            }
                            else if (typeEle != null && typeEle is WallType)
                            {
                                wallType = typeEle as WallType;
                                builtInCategory = (BuiltInCategory)wallType.Category.Id.IntegerValue;
                            }
                            else if (typeEle != null && typeEle is DuctType)
                            {
                                ductType = typeEle as DuctType;
                                builtInCategory = (BuiltInCategory)ductType.Category.Id.IntegerValue;
                            }
                        }
                    }
                    if (builtInCategory == BuiltInCategory.INVALID)
                        continue;
                    switch (builtInCategory)
                    {
                        case BuiltInCategory.OST_Walls:
                            if (wallType == null)
                            {
                                using (Transaction transaction = new Transaction(doc, "Create Wall Type"))
                                {
                                    transaction.Start();
                                    wallType = CreateOrGetWallType(doc, data.Thickness / 304.8);
                                    transaction.Commit();
                                }
                                if (wallType == null)
                                    continue;
                            }
                            break;
                        case BuiltInCategory.OST_DuctCurves:
                            if (ductType == null)
                            {
                                using (Transaction transaction = new Transaction(doc, "Create Duct Type"))
                                {
                                    transaction.Start();
                                    ductType = CreateOrGetDuctType(doc, data.Thickness / 304.8, data.Height / 304.8);
                                    transaction.Commit();
                                }
                                if (ductType == null)
                                    continue;
                            }
                            break;
                        default:
                            if (symbol == null)
                            {
                                symbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(builtInCategory)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault(fs => fs.IsActive); // Get active type as default type
                                if (symbol == null)
                                {
                                    symbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(builtInCategory)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault();
                                }
                            }
                            if (symbol == null)
                                continue;
                            break;
                    }

                    // Step3 Call generic method to create family instance
                    using (Transaction transaction = new Transaction(doc, "Create Linear Component"))
                    {
                        transaction.Start();
                        switch (builtInCategory)
                        {
                            case BuiltInCategory.OST_Walls:
                                Wall wall = null;
                                wall = Wall.Create
                                (
                                  doc,
                                  JZLine.ToLine(data.LocationLine),
                                  wallType.Id,
                                  baseLevel.Id,
                                  data.Height / 304.8,
                                  baseOffset,
                                  false,
                                  false
                                );
                                if (wall != null)
                                {
                                    elementIds.Add(wall.Id.IntegerValue);
                                }
                                break;
                            case BuiltInCategory.OST_DuctCurves:
                                Duct duct = null;
                                // Get MEP system type (required)
                                MEPSystemType mepSystemType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(MEPSystemType))
                                    .Cast<MEPSystemType>()
                                    .FirstOrDefault(m => m.SystemClassification == MEPSystemClassification.SupplyAir);

                                if (mepSystemType != null)
                                {
                                    duct = Duct.Create(
                                        doc,
                                        mepSystemType.Id,
                                        ductType.Id,
                                        baseLevel.Id,
                                        JZLine.ToLine(data.LocationLine).GetEndPoint(0),
                                        JZLine.ToLine(data.LocationLine).GetEndPoint(1)
                                    );

                                    if (duct != null)
                                    {
                                        // Set height offset
                                        Parameter offsetParam = duct.get_Parameter(BuiltInParameter.RBS_OFFSET_PARAM);
                                        if (offsetParam != null)
                                            offsetParam.Set(baseOffset);
                                        elementIds.Add(duct.Id.IntegerValue);
                                    }
                                }
                                break;
                            default:
                                if (!symbol.IsActive)
                                    symbol.Activate();

                                // Call FamilyInstance generic creation method
                                var instance = doc.CreateInstance(symbol, null, JZLine.ToLine(data.LocationLine), baseLevel, topLevel, baseOffset, topOffset);
                                if (instance != null)
                                {
                                    elementIds.Add(instance.Id.IntegerValue);
                                }
                                break;
                        }
                        //doc.Refresh();
                        transaction.Commit();
                    }
                }
                Result = new AIResult<List<int>>
                {
                    Success = true,
                    Message = $"Successfully created {elementIds.Count} family instances, their ElementIds are stored in the Response property",
                    Response = elementIds,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<int>>
                {
                    Success = false,
                    Message = $"Error creating linear components: {ex.Message}",
                };
                TaskDialog.Show("Error", $"Error creating linear components: {ex.Message}");
            }
            finally
            {
                _resetEvent.Set(); // Notify waiting thread that operation is complete
            }
        }

        /// <summary>
        /// Wait for creation completion
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout duration (milliseconds)</param>
        /// <returns>Whether the operation completed before timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Create Linear Components";
        }

        /// <summary>
        /// Create or get wall type with specified thickness
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="width">Width (ft)</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private WallType CreateOrGetWallType(Document doc, double width = 200 / 304.8)
        {
            // If no valid type exists
            // First search for architectural wall type with specified thickness
            WallType existingType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault(w => w.Name == $"{_wallName}{width * 304.8}mm");
            if (existingType != null)
                return existingType;

            // If not exists, create new wall type based on basic wall
            WallType baseWallType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault(w => w.Name.Contains("Generic")); ;
            if (baseWallType == null)
            {
                baseWallType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault(); ;
            }

            if (baseWallType == null)
                throw new InvalidOperationException("No available base wall type found");

            // Duplicate wall type
            WallType newWallType = null;
            newWallType = baseWallType.Duplicate($"{_wallName}{width * 304.8}mm") as WallType;

            // Set wall thickness
            CompoundStructure cs = newWallType.GetCompoundStructure();
            if (cs != null)
            {
                // Get material ID of original layer
                ElementId materialId = cs.GetLayers().First().MaterialId;

                // Create new single layer structure
                CompoundStructureLayer newLayer = new CompoundStructureLayer(
                    width,  // Width (converted to feet)
                    MaterialFunctionAssignment.Structure,  // Function assignment
                    materialId  // Material ID
                );

                // Create new compound structure
                IList<CompoundStructureLayer> newLayers = new List<CompoundStructureLayer> { newLayer };
                cs.SetLayers(newLayers);

                // Apply new compound structure
                newWallType.SetCompoundStructure(cs);
            }
            return newWallType;
        }

        /// <summary>
        /// Create or get duct type with specified dimensions
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="width">Width (ft)</param>
        /// <param name="height">Height (ft)</param>
        /// <returns>Duct type</returns>
        private DuctType CreateOrGetDuctType(Document doc, double width, double height)
        {
            string typeName = $"{_ductName}{width * 304.8}x{height * 304.8}mm";

            // First search for duct type with specified dimensions
            DuctType existingType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(DuctType))
                                    .Cast<DuctType>()
                                    .FirstOrDefault(d => d.Name == typeName && d.Shape == ConnectorProfileType.Rectangular);

            if (existingType != null)
                return existingType;

            // If not exists, create new duct type based on existing rectangular duct type
            DuctType baseDuctType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(DuctType))
                                    .Cast<DuctType>()
                                    .FirstOrDefault(d => d.Shape == ConnectorProfileType.Rectangular);

            if (baseDuctType == null)
                throw new InvalidOperationException("No available base rectangular duct type found");

            // Duplicate duct type
            DuctType newDuctType = baseDuctType.Duplicate(typeName) as DuctType;

            // Set duct dimension parameters
            Parameter widthParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            Parameter heightParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

            if (widthParam != null && heightParam != null)
            {
                widthParam.Set(width);
                heightParam.Set(height);
            }

            return newDuctType;
        }

    }
}
