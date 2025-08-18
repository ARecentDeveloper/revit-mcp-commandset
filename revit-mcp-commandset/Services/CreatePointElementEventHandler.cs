using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services
{
    public class CreatePointElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        public List<PointElement> CreatedInfo { get; private set; }
        /// <summary>
        /// Execution result (output data)
        /// </summary>
        public AIResult<List<int>> Result { get; private set; }

        /// <summary>
        /// Set creation parameters
        /// </summary>
        public void SetParameters(List<PointElement> data)
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
                        }
                    }
                    if (builtInCategory == BuiltInCategory.INVALID)
                        continue;
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

                    // Step3 Call generic method to create family instance
                    using (Transaction transaction = new Transaction(doc, "Create Point-based Components"))
                    {
                        transaction.Start();

                        if (!symbol.IsActive)
                            symbol.Activate();

                        // Call FamilyInstance generic creation method
                        var instance = doc.CreateInstance(symbol, JZPoint.ToXYZ(data.LocationPoint), null, baseLevel, topLevel, baseOffset, topOffset);
                        if (instance != null)
                        {
                            if (builtInCategory == BuiltInCategory.OST_Doors)
                            {
                                // Flip door to ensure proper cutting
                                instance.flipFacing();
                                doc.Regenerate();
                                instance.flipFacing();
                                doc.Regenerate();
                            }

                            elementIds.Add((int)instance.Id.Value);
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
                    Message = $"Error creating point-based components: {ex.Message}",
                };
                TaskDialog.Show("Error", $"Error creating point-based components: {ex.Message}");
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
            return "Create Point-based Components";
        }

    }
}
