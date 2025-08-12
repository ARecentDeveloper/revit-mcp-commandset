using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.DB.Structure;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class PlaceHangersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        public object Results { get; private set; }

        // Parameters
        private string _placementMode;
        private string _hangerType;
        private string _targetCategory;
        private string _spacing;
        private string _startOffset;
        private string _endOffset;

        public void Reset()
        {
            _resetEvent.Reset();
            Results = null;
        }

        public void SetParameters(string placementMode, string hangerType, string targetCategory, 
            string spacing, string startOffset, string endOffset)
        {
            _placementMode = placementMode;
            _hangerType = hangerType;
            _targetCategory = targetCategory;
            _spacing = spacing;
            _startOffset = startOffset;
            _endOffset = endOffset;
        }

        // Helper methods
        private double ParseLength(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0.0;
            
            s = s.Trim().Replace("\"", "").Replace("'", "");
            double feet = 0.0, inches = 0.0;

            if (s.Contains("'") || s.Contains("\""))
            {
                if (s.Contains("'"))
                {
                    var parts = Regex.Split(s, @"[''\s]+").Where(p => !string.IsNullOrEmpty(p)).ToArray();
                    if (parts.Length > 0) feet = double.Parse(parts[0]);
                    if (parts.Length > 1) inches = double.Parse(parts[1].Replace("\"", ""));
                }
                else if (s.Contains("\""))
                {
                    inches = double.Parse(s.Replace("\"", ""));
                }
            }
            else
            {
                var parts = s.Split(' ');
                if (parts.Length >= 2)
                {
                    feet = double.Parse(parts[0]);
                    inches = double.Parse(parts[1]);
                }
                else
                {
                    feet = double.Parse(s);
                }
            }
            return feet + inches / 12.0;
        }   
     private double GetUpperRodHeight(double z, List<Level> levels)
        {
            var upperLevels = levels.Where(lvl => lvl.Elevation > z).OrderBy(lvl => lvl.Elevation);
            return upperLevels.Any() ? upperLevels.First().Elevation - z : 10.0;
        }

        private FamilySymbol GetFamilySymbol(Document doc, string familyName)
        {
            var collector = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).WhereElementIsElementType();
            foreach (FamilySymbol fs in collector)
            {
                if (fs.Family?.Name == familyName)
                {
                    if (!fs.IsActive)
                    {
                        fs.Activate();
                        doc.Regenerate();
                    }
                    return fs;
                }
            }
            return null;
        }

        private class CategorySelectionFilter : ISelectionFilter
        {
            private readonly ElementId _categoryId;

            public CategorySelectionFilter(BuiltInCategory bic, Document doc)
            {
                _categoryId = Category.GetCategory(doc, bic).Id;
            }

            public bool AllowElement(Element elem)
            {
                return elem.Category?.Id == _categoryId;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                UIDocument uidoc = app.ActiveUIDocument;

                if (_placementMode == "click")
                {
                    Results = ExecuteClickMode(doc, uidoc);
                }
                else if (_placementMode == "parametric")
                {
                    Results = ExecuteParametricMode(doc, uidoc);
                }
                else
                {
                    throw new System.ArgumentException($"Invalid placement mode: {_placementMode}");
                }
            }
            catch (Exception ex)
            {
                Results = new
                {
                    success = false,
                    message = ex.Message,
                    count = 0
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }   
     private object ExecuteClickMode(Document doc, UIDocument uidoc)
        {
            // Determine target category
            var targetBic = DetermineTargetCategory(doc, uidoc);
            var targetFilter = new CategorySelectionFilter(targetBic, doc);

            // Get family symbol
            var symbol = GetFamilySymbol(doc, _hangerType);
            if (symbol == null)
            {
                throw new Exception($"Family '{_hangerType}' not found or not loaded");
            }

            // Get levels for rod height calculation
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            if (!levels.Any())
            {
                throw new Exception("No levels found in the project");
            }

            int count = 0;
            var placedHangerIds = new List<ElementId>();

            while (true)
            {
                Reference pickedRef;
                try
                {
                    string categoryName = targetBic == BuiltInCategory.OST_PipeCurves ? "Pipe" : "Conduit";
                    pickedRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        targetFilter,
                        $"Click on a {categoryName} to place a {_hangerType} (ESC to finish)"
                    );
                }
                catch (System.OperationCanceledException)
                {
                    // User pressed ESC to finish - this is normal completion
                    break;
                }

                if (pickedRef != null)
                {
                    // Create a separate transaction for each hanger placement
                    using (var trans = new Transaction(doc, $"Place {_hangerType}"))
                    {
                        trans.Start();
                        try
                        {
                            var elem = doc.GetElement(pickedRef.ElementId);
                            if (PlaceHangerOnElement(doc, elem, pickedRef.GlobalPoint, symbol, levels))
                            {
                                count++;
                                trans.Commit();
                            }
                            else
                            {
                                trans.RollBack();
                            }
                        }
                        catch (Exception)
                        {
                            trans.RollBack();
                            // Continue with next placement instead of throwing
                        }
                    }
                }
            }

            return new
            {
                success = true,
                message = $"Successfully placed {count} {_hangerType}(s) by clicking",
                count = count,
                mode = "click"
            };
        } 
       private object ExecuteParametricMode(Document doc, UIDocument uidoc)
        {
            // Determine target category and get elements
            var targetBic = DetermineTargetCategory(doc, uidoc);
            var elements = GetTargetElements(doc, uidoc, targetBic);

            if (!elements.Any())
            {
                throw new Exception("No valid elements selected or found");
            }

            // Get family symbol
            var symbol = GetFamilySymbol(doc, _hangerType);
            if (symbol == null)
            {
                throw new Exception($"Family '{_hangerType}' not found or not loaded");
            }

            // Parse spacing and offsets
            double spacing = ParseLength(_spacing);
            double startOffset = ParseLength(_startOffset);
            double endOffset = ParseLength(_endOffset);

            // Get levels for rod height calculation
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            levels = levels.OrderBy(lv => lv.Elevation).ToList();

            int count = 0;

            using (var trans = new Transaction(doc, $"Place {_hangerType}s Parametrically"))
            {
                trans.Start();

                try
                {
                    foreach (var elem in elements)
                    {
                        if (!(elem.Location is LocationCurve locationCurve))
                            continue;

                        var curve = locationCurve.Curve;
                        if (curve == null) continue;

                        var start = curve.GetEndPoint(0);
                        var end = curve.GetEndPoint(1);
                        var dz = end.Z - start.Z;
                        var horizontal = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));

                        // Skip vertical elements or elements with slope > 30%
                        if (horizontal == 0 || Math.Abs(dz / horizontal) > 0.3)
                            continue;

                        double totalLen = curve.Length;
                        double runLen = totalLen - startOffset - endOffset;
                        if (runLen <= 0) continue;

                        int num = (int)(runLen / spacing) + 1;

                        // Calculate rotation angle
                        var direction = (end - start).Normalize();
                        var horizDir = new XYZ(direction.X, direction.Y, 0);
                        double angle = 0;

                        if (!horizDir.IsZeroLength())
                        {
                            horizDir = horizDir.Normalize();
                            angle = XYZ.BasisX.AngleTo(horizDir);
                            if (horizDir.CrossProduct(XYZ.BasisX).Z < 0)
                                angle = -angle;
                        }

                        // Place hangers along the curve
                        for (int i = 0; i < num; i++)
                        {
                            double dist = startOffset + i * spacing;
                            var pt = curve.Evaluate(dist / totalLen, true);

                            if (PlaceHangerAtPoint(doc, elem, pt, symbol, levels, angle))
                            {
                                count++;
                            }
                        }
                    }

                    trans.Commit();
                }
                catch (Exception)
                {
                    trans.RollBack();
                    throw;
                }
            }

            return new
            {
                success = true,
                message = $"Successfully placed {count} {_hangerType}(s) parametrically",
                count = count,
                mode = "parametric",
                spacing = _spacing,
                startOffset = _startOffset,
                endOffset = _endOffset
            };
        }     
   private BuiltInCategory DetermineTargetCategory(Document doc, UIDocument uidoc)
        {
            if (!string.IsNullOrEmpty(_targetCategory))
            {
                return _targetCategory == "Pipe" ? BuiltInCategory.OST_PipeCurves : BuiltInCategory.OST_Conduit;
            }

            // Auto-detect from selection
            var selIds = uidoc.Selection.GetElementIds();
            var selectedElements = selIds.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();

            var pipeCatId = Category.GetCategory(doc, BuiltInCategory.OST_PipeCurves).Id;
            var condCatId = Category.GetCategory(doc, BuiltInCategory.OST_Conduit).Id;

            bool hasPipes = selectedElements.Any(e => e.Category?.Id == pipeCatId);
            bool hasConds = selectedElements.Any(e => e.Category?.Id == condCatId);

            if (hasPipes && !hasConds)
                return BuiltInCategory.OST_PipeCurves;
            else if (hasConds && !hasPipes)
                return BuiltInCategory.OST_Conduit;
            else
                return BuiltInCategory.OST_PipeCurves; // Default to pipes
        }

        private List<Element> GetTargetElements(Document doc, UIDocument uidoc, BuiltInCategory targetBic)
        {
            var selIds = uidoc.Selection.GetElementIds();
            var targetCatId = Category.GetCategory(doc, targetBic).Id;

            var selectedElements = selIds
                .Select(id => doc.GetElement(id))
                .Where(e => e?.Category?.Id == targetCatId)
                .ToList();

            if (selectedElements.Any())
                return selectedElements;

            // If no selection, get all elements of the target category in the active view
            var collector = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(targetBic)
                .WhereElementIsNotElementType();

            return collector.ToList();
        }

        private bool PlaceHangerOnElement(Document doc, Element elem, XYZ clickPoint, FamilySymbol symbol, List<Level> levels)
        {
            if (!(elem.Location is LocationCurve locationCurve) || locationCurve.Curve == null)
                return false;

            var curve = locationCurve.Curve;
            
            // Project click point onto curve
            var projectionResult = curve.Project(clickPoint);
            if (projectionResult == null) return false;

            var pt = projectionResult.XYZPoint;
            
            // Calculate rotation angle
            double angle = 0;
            if (curve.IsBound)
            {
                var start = curve.GetEndPoint(0);
                var end = curve.GetEndPoint(1);
                var direction = (end - start);

                if (Math.Abs(direction.X) > 1e-9 || Math.Abs(direction.Y) > 1e-9)
                {
                    var horizDir = new XYZ(direction.X, direction.Y, 0).Normalize();
                    angle = XYZ.BasisX.AngleTo(horizDir);
                    if (horizDir.Y < 0) angle = -angle;
                }
            }

            return PlaceHangerAtPoint(doc, elem, pt, symbol, levels, angle);
        }       
        private bool PlaceHangerAtPoint(Document doc, Element elem, XYZ pt, FamilySymbol symbol, List<Level> levels, double angle)
        {
            try
            {
                // Calculate rod height
                double rodHeight = GetUpperRodHeight(pt.Z, levels);

                // Get diameter parameters
                var isPipe = elem.Category.Id == Category.GetCategory(doc, BuiltInCategory.OST_PipeCurves).Id;
                var outerDiamParam = isPipe ? BuiltInParameter.RBS_PIPE_OUTER_DIAMETER : BuiltInParameter.RBS_CONDUIT_OUTER_DIAM_PARAM;
                var diamParam = isPipe ? BuiltInParameter.RBS_PIPE_DIAMETER_PARAM : BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM;

                var outerDiameter = elem.get_Parameter(outerDiamParam)?.AsDouble() ?? 0;
                var diameter = elem.get_Parameter(diamParam)?.AsDouble() ?? 0;

                if (outerDiameter == 0 || diameter == 0) return false;

                // Create hanger instance
                var inst = doc.Create.NewFamilyInstance(pt, symbol, elem, StructuralType.NonStructural);
                if (inst == null) return false;

                // Set parameters
                SetHangerParameters(inst, rodHeight, outerDiameter, diameter);

                // Rotate instance
                var rotationAngle = angle;
                if (_hangerType == "Trapeze Hanger")
                    rotationAngle += Math.PI / 2; // Add 90 degrees for trapeze

                if (Math.Abs(rotationAngle) > 1e-6)
                {
                    var axis = Line.CreateBound(pt, pt + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, inst.Id, axis, rotationAngle);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SetHangerParameters(FamilyInstance inst, double rodHeight, double outerDiameter, double diameter)
        {
            var rodParamName = _hangerType == "Clevis Hanger" ? "HPH_Anchor Elevation" : "Anchor Elevation";
            var diamParamName = _hangerType == "Clevis Hanger" ? "Nominal Diameter" : null;
            var outerDiamParamName = _hangerType == "Trapeze Hanger" ? "Outside Pipe Diameter" : null;
            var trapezeHeightParamName = _hangerType == "Trapeze Hanger" ? "Height Diameter" : null;

            // Set rod height
            var rodParam = inst.LookupParameter(rodParamName);
            if (rodParam != null && !rodParam.IsReadOnly)
                rodParam.Set(rodHeight);

            // Set diameter parameters
            if (!string.IsNullOrEmpty(diamParamName))
            {
                var param = inst.LookupParameter(diamParamName);
                if (param != null && !param.IsReadOnly)
                    param.Set(diameter);
            }

            if (!string.IsNullOrEmpty(outerDiamParamName))
            {
                var param = inst.LookupParameter(outerDiamParamName);
                if (param != null && !param.IsReadOnly)
                    param.Set(outerDiameter);
            }

            if (!string.IsNullOrEmpty(trapezeHeightParamName))
            {
                var param = inst.LookupParameter(trapezeHeightParamName);
                if (param != null && !param.IsReadOnly)
                    param.Set(outerDiameter);
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Place Hangers";
        }
    }
}