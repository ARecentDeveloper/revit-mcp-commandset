using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class CreateSurfaceElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        public List<SurfaceElement> CreatedInfo { get; private set; }
        /// <summary>
        /// Execution result (output data)
        /// </summary>
        public AIResult<List<int>> Result { get; private set; }
        public string _floorName = "Generic - ";
        public bool _structural = true;

        /// <summary>
        /// Set creation parameters
        /// </summary>
        public void SetParameters(List<SurfaceElement> data)
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
                    Enum.TryParse(data.Category.Replace(".", "").Replace("BuiltInCategory", ""), true, out builtInCategory);

                    // Step1 Get level and offset
                    Level baseLevel = null;
                    Level topLevel = null;
                    double topOffset = -1;  // ft
                    double baseOffset = -1; // ft
                    baseLevel = doc.FindNearestLevel(data.BaseLevel / 304.8);
                    baseOffset = (data.BaseOffset + data.BaseLevel) / 304.8 - baseLevel.Elevation;
                    topLevel = doc.FindNearestLevel((data.BaseLevel + data.BaseOffset + data.Thickness) / 304.8);
                    topOffset = (data.BaseLevel + data.BaseOffset + data.Thickness) / 304.8 - topLevel.Elevation;
                    if (baseLevel == null)
                        continue;

                    // Step2 Get family type
                    FamilySymbol symbol = null;
                    FloorType floorType = null;
                    if (data.TypeId != -1 && data.TypeId != 0)
                    {
                        ElementId typeELeId = new ElementId((long)data.TypeId);
                        if (typeELeId != null)
                        {
                            Element typeEle = doc.GetElement(typeELeId);
                            if (typeEle != null && typeEle is FamilySymbol)
                            {
                                symbol = typeEle as FamilySymbol;
                                // Get symbol's Category object and convert to BuiltInCategory enum
                                builtInCategory = (BuiltInCategory)symbol.Category.Id.Value;
                            }
                            else if (typeEle != null && typeEle is FloorType)
                            {
                                floorType = typeEle as FloorType;
                                builtInCategory = (BuiltInCategory)floorType.Category.Id.Value;
                            }
                        }
                    }
                    if (builtInCategory == BuiltInCategory.INVALID)
                        continue;
                    switch (builtInCategory)
                    {
                        case BuiltInCategory.OST_Floors:
                            if (floorType == null)
                            {
                                using (Transaction transaction = new Transaction(doc, "Create Floor Type"))
                                {
                                    transaction.Start();
                                    floorType = CreateOrGetFloorType(doc, data.Thickness / 304.8);
                                    transaction.Commit();
                                }
                                if (floorType == null)
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

                    // Step3 Batch create floors
                    Floor floor = null;
                    using (Transaction transaction = new Transaction(doc, "Create Surface-based Components"))
                    {
                        transaction.Start();

                        switch (builtInCategory)
                        {
                            case BuiltInCategory.OST_Floors:
                                CurveArray curves = new CurveArray();
                                foreach (var jzLine in data.Boundary.OuterLoop)
                                {
                                    curves.Append(JZLine.ToLine(jzLine));
                                }
                                CurveLoop curveLoop = CurveLoop.Create(data.Boundary.OuterLoop.Select(l => JZLine.ToLine(l) as Curve).ToList());

                                // Multi-version
#if REVIT2022_OR_GREATER
                                floor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, baseLevel.Id);
#else
                                floor = doc.Create.NewFloor(curves, floorType, baseLevel, _structural);
#endif
                                //Edit floor parameters
                                if (floor != null)
                                {
                                    floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(baseOffset);
                                    elementIds.Add((int)floor.Id.Value);
                                }
                                break;
                            default:

                                break;
                        }

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
                    Message = $"Error creating surface-based components: {ex.Message}",
                };
                TaskDialog.Show("Error", $"Error creating surface-based components: {ex.Message}");
            }
            finally
            {
                _resetEvent.Set(); // Notify waiting thread that operation is complete
            }
        }

        /// <summary>
        /// Wait for creation completion
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds</param>
        /// <returns>Whether operation completed before timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Create Surface-based Components";
        }

        /// <summary>
        /// Get or create floor type with specified thickness
        /// </summary>
        /// <param name="thickness">Target thickness (ft)</param>
        /// <returns>Floor type that meets thickness requirements</returns>
        private FloorType CreateOrGetFloorType(Document doc, double thickness = 200 / 304.8)
        {

            // Find floor type matching thickness
            FloorType existingType = new FilteredElementCollector(doc)
                                     .OfClass(typeof(FloorType))                    // Get only FloorType class
                                     .OfCategory(BuiltInCategory.OST_Floors)        // Get only floor category
                                     .Cast<FloorType>()                            // Convert to FloorType
                                     .FirstOrDefault(w => w.Name == $"{_floorName}{thickness * 304.8}mm");
            if (existingType != null)
                return existingType;
            // If no matching floor type found, create new one
            FloorType baseFloorType = existingType = new FilteredElementCollector(doc)
                                     .OfClass(typeof(FloorType))                    // Get only FloorType class
                                     .OfCategory(BuiltInCategory.OST_Floors)        // Get only floor category
                                     .Cast<FloorType>()                            // Convert to FloorType
                                     .FirstOrDefault(w => w.Name.Contains("Generic"));
            if (existingType != null)
            {
                baseFloorType = existingType = new FilteredElementCollector(doc)
                                     .OfClass(typeof(FloorType))                    // Get only FloorType class
                                     .OfCategory(BuiltInCategory.OST_Floors)        // Get only floor category
                                     .Cast<FloorType>()                            // Convert to FloorType
                                     .FirstOrDefault();
            }

            // Copy floor type
            FloorType newFloorType = null;
            newFloorType = baseFloorType.Duplicate($"{_floorName}{thickness * 304.8}mm") as FloorType;

            // Set thickness of new floor type
            // Get compound structure settings
            CompoundStructure cs = newFloorType.GetCompoundStructure();
            if (cs != null)
            {
                // Get all layers
                IList<CompoundStructureLayer> layers = cs.GetLayers();
                if (layers.Count > 0)
                {
                    // Calculate current total thickness
                    double currentTotalThickness = cs.GetWidth();

                    // Adjust each layer thickness proportionally
                    for (int i = 0; i < layers.Count; i++)
                    {
                        CompoundStructureLayer layer = layers[i];
                        double newLayerThickness = thickness;
                        cs.SetLayerWidth(i, newLayerThickness);
                    }

                    // Apply modified compound structure settings
                    newFloorType.SetCompoundStructure(cs);
                }
            }
            return newFloorType;
        }

    }
}
