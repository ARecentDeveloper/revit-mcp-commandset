using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.PACompliance;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Service for performing PA compliance element modifications
    /// </summary>
    public static class PAElementModificationService
    {
        /// <summary>
        /// Rename a family element
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="elementId">Element ID</param>
        /// <param name="newName">New name</param>
        /// <param name="dryRun">Whether this is a dry run</param>
        /// <returns>Operation result</returns>
        public static PAComplianceItemResult RenameFamilyElement(Document doc, string elementId, string newName, bool dryRun = false)
        {
            var result = new PAComplianceItemResult
            {
                ItemId = elementId,
                TargetValue = newName,
                OperationType = PAComplianceOperationType.Rename
            };

            try
            {
                // Validate input parameters
                if (string.IsNullOrWhiteSpace(elementId))
                {
                    throw new ArgumentException("Element ID cannot be null or empty");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new ArgumentException("New name cannot be null or empty");
                }

                if (!int.TryParse(elementId, out int id))
                {
                    PAComplianceLoggingService.LogValidationError("ElementId", $"Invalid element ID format: {elementId}");
                    throw new ArgumentException($"Invalid element ID: {elementId}");
                }

                var revitElementId = new ElementId((long)id);
                var element = doc.GetElement(revitElementId);

                if (element == null)
                {
                    PAComplianceLoggingService.LogValidationError("ElementExists", $"Element with ID {elementId} not found");
                    throw new InvalidOperationException($"Element with ID {elementId} not found");
                }

                result.ItemName = element.Name;
                result.CurrentValue = element.Name;

                // Log permission validation
                var canRename = CanRenameElement(element);
                PAComplianceLoggingService.LogPermissionValidation(elementId, element.GetType().Name, "CanRename", canRename, 
                    canRename ? "Element can be renamed" : "Element cannot be renamed (system element or locked)");

                if (dryRun)
                {
                    result.Success = canRename;
                    result.ErrorMessage = canRename ? "Dry run - would rename element" : "Element cannot be renamed";
                    PAComplianceLoggingService.LogElementModification(elementId, element.GetType().Name, PAComplianceOperationType.Rename, 
                        result.CurrentValue, newName, result.Success, result.ErrorMessage);
                    return result;
                }

                // Check if element can be renamed
                if (!canRename)
                {
                    throw new InvalidOperationException($"Element {element.Name} cannot be renamed (may be system element or locked)");
                }

                // Perform the rename operation
                if (element is Family family)
                {
                    RenameFamilyWithTransaction(doc, family, newName);
                }
                else if (element is FamilySymbol familySymbol)
                {
                    RenameFamilySymbolWithTransaction(doc, familySymbol, newName);
                }
                else if (element is ViewSheet sheet)
                {
                    RenameSheetWithTransaction(doc, sheet, newName);
                }
                else
                {
                    RenameGenericElementWithTransaction(doc, element, newName);
                }

                result.Success = true;
                PAComplianceLoggingService.LogElementModification(elementId, element.GetType().Name, PAComplianceOperationType.Rename, 
                    result.CurrentValue, newName, true);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                PAComplianceLoggingService.LogElementModification(elementId, "Unknown", PAComplianceOperationType.Rename, 
                    result.CurrentValue ?? "Unknown", newName, false, ex.Message);
                System.Diagnostics.Trace.WriteLine($"Error renaming element {elementId}: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Rename a workset
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="worksetId">Workset ID</param>
        /// <param name="newName">New name</param>
        /// <param name="dryRun">Whether this is a dry run</param>
        /// <returns>Operation result</returns>
        public static PAComplianceItemResult RenameWorkset(Document doc, int worksetId, string newName, bool dryRun = false)
        {
            var result = new PAComplianceItemResult
            {
                ItemId = worksetId.ToString(),
                TargetValue = newName,
                OperationType = PAComplianceOperationType.Rename
            };

            try
            {
                if (!doc.IsWorkshared)
                {
                    throw new InvalidOperationException("Document is not workshared");
                }

                var worksetIdObj = new WorksetId(worksetId);
                var worksetTable = doc.GetWorksetTable();
                var workset = worksetTable.GetWorkset(worksetIdObj);

                if (workset == null)
                {
                    throw new InvalidOperationException($"Workset with ID {worksetId} not found");
                }

                result.ItemName = workset.Name;
                result.CurrentValue = workset.Name;

                if (dryRun)
                {
                    result.Success = true;
                    result.ErrorMessage = "Dry run - would rename workset";
                    return result;
                }

                // Check if workset can be renamed
                if (workset.Kind != WorksetKind.UserWorkset)
                {
                    throw new InvalidOperationException($"Workset {workset.Name} is a system workset and cannot be renamed");
                }

                // Perform the rename operation
                RenameWorksetWithTransaction(doc, worksetIdObj, newName);

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                System.Diagnostics.Trace.WriteLine($"Error renaming workset {worksetId}: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Change element category (for model integrity fixes)
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="elementId">Element ID</param>
        /// <param name="newCategoryName">New category name</param>
        /// <param name="dryRun">Whether this is a dry run</param>
        /// <returns>Operation result</returns>
        public static PAComplianceItemResult ChangeElementCategory(Document doc, string elementId, string newCategoryName, bool dryRun = false)
        {
            var result = new PAComplianceItemResult
            {
                ItemId = elementId,
                TargetValue = newCategoryName,
                OperationType = PAComplianceOperationType.Recategorize
            };

            try
            {
                if (!int.TryParse(elementId, out int id))
                {
                    throw new ArgumentException($"Invalid element ID: {elementId}");
                }

                var revitElementId = new ElementId((long)id);
                var element = doc.GetElement(revitElementId);

                if (element == null)
                {
                    throw new InvalidOperationException($"Element with ID {elementId} not found");
                }

                result.ItemName = element.Name;
                result.CurrentValue = element.Category?.Name ?? "Unknown";

                if (dryRun)
                {
                    result.Success = true;
                    result.ErrorMessage = "Dry run - would change category (may require manual intervention)";
                    return result;
                }

                // Category changes are complex and often require manual intervention
                // For now, we'll mark these as requiring manual review
                result.Success = true;
                result.ErrorMessage = "Category change requires manual review - automatic category changes are not supported for all element types";
                
                System.Diagnostics.Trace.WriteLine($"Category change for element {elementId} from {result.CurrentValue} to {newCategoryName} requires manual review");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                System.Diagnostics.Trace.WriteLine($"Error changing category for element {elementId}: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Perform batch element modifications with transaction management
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="operations">List of operations to perform</param>
        /// <param name="dryRun">Whether this is a dry run</param>
        /// <returns>List of operation results</returns>
        public static List<PAComplianceItemResult> PerformBatchModifications(Document doc, List<PAElementModificationOperation> operations, bool dryRun = false)
        {
            var results = new List<PAComplianceItemResult>();

            if (dryRun)
            {
                // For dry run, process each operation individually without transactions
                foreach (var operation in operations)
                {
                    var result = ProcessSingleOperation(doc, operation, true);
                    results.Add(result);
                }
                return results;
            }

            // Group operations by type for efficient transaction management
            var familyRenames = operations.Where(o => o.OperationType == PAComplianceOperationType.Rename && 
                                                     (o.ElementType == "Family" || o.ElementType == "FamilySymbol")).ToList();
            var sheetRenames = operations.Where(o => o.OperationType == PAComplianceOperationType.Rename && 
                                                    o.ElementType == "ViewSheet").ToList();
            var worksetRenames = operations.Where(o => o.OperationType == PAComplianceOperationType.Rename && 
                                                      o.ElementType == "Workset").ToList();
            var categoryChanges = operations.Where(o => o.OperationType == PAComplianceOperationType.Recategorize).ToList();

            // Process family renames in batch
            if (familyRenames.Any())
            {
                var batchResults = ProcessFamilyRenamesBatch(doc, familyRenames);
                results.AddRange(batchResults);
            }

            // Process sheet renames in batch
            if (sheetRenames.Any())
            {
                var batchResults = ProcessSheetRenamesBatch(doc, sheetRenames);
                results.AddRange(batchResults);
            }

            // Process workset renames in batch
            if (worksetRenames.Any())
            {
                var batchResults = ProcessWorksetRenamesBatch(doc, worksetRenames);
                results.AddRange(batchResults);
            }

            // Process category changes individually (they're complex)
            foreach (var operation in categoryChanges)
            {
                var result = ProcessSingleOperation(doc, operation, false);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Check if an element can be renamed
        /// </summary>
        private static bool CanRenameElement(Element element)
        {
            try
            {
                // Check if element is null
                if (element == null) return false;

                // Element read-only check not available in this Revit version

                // Check specific element types
                if (element is Family family)
                {
                    // System families typically cannot be renamed
                    // IsSystemFamily property not available in this Revit version
                    return true;
                }

                if (element is FamilySymbol familySymbol)
                {
                    // Check if the parent family can be modified
                    // IsSystemFamily property not available in this Revit version
                    return familySymbol.Family != null;
                }

                if (element is ViewSheet)
                {
                    // Sheets can typically be renamed
                    return true;
                }

                // For other element types, assume they can be renamed unless proven otherwise
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Rename family with transaction
        /// </summary>
        private static void RenameFamilyWithTransaction(Document doc, Family family, string newName)
        {
            using (var trans = new Transaction(doc, "Rename Family"))
            {
                trans.Start();
                try
                {
                    family.Name = newName;
                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Rename family symbol with transaction
        /// </summary>
        private static void RenameFamilySymbolWithTransaction(Document doc, FamilySymbol familySymbol, string newName)
        {
            using (var trans = new Transaction(doc, "Rename Family Symbol"))
            {
                trans.Start();
                try
                {
                    familySymbol.Name = newName;
                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Rename sheet with transaction
        /// </summary>
        private static void RenameSheetWithTransaction(Document doc, ViewSheet sheet, string newName)
        {
            using (var trans = new Transaction(doc, "Rename Sheet"))
            {
                trans.Start();
                try
                {
                    sheet.Name = newName;
                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Rename generic element with transaction
        /// </summary>
        private static void RenameGenericElementWithTransaction(Document doc, Element element, string newName)
        {
            using (var trans = new Transaction(doc, "Rename Element"))
            {
                trans.Start();
                try
                {
                    element.Name = newName;
                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Rename workset with transaction
        /// </summary>
        private static void RenameWorksetWithTransaction(Document doc, WorksetId worksetId, string newName)
        {
            using (var trans = new Transaction(doc, "Rename Workset"))
            {
                trans.Start();
                try
                {
                    WorksetTable.RenameWorkset(doc, worksetId, newName);
                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Process a single operation
        /// </summary>
        private static PAComplianceItemResult ProcessSingleOperation(Document doc, PAElementModificationOperation operation, bool dryRun)
        {
            switch (operation.OperationType)
            {
                case PAComplianceOperationType.Rename:
                    if (operation.ElementType == "Workset")
                    {
                        return RenameWorkset(doc, int.Parse(operation.ElementId), operation.NewValue, dryRun);
                    }
                    else
                    {
                        return RenameFamilyElement(doc, operation.ElementId, operation.NewValue, dryRun);
                    }

                case PAComplianceOperationType.Recategorize:
                    return ChangeElementCategory(doc, operation.ElementId, operation.NewValue, dryRun);

                default:
                    return new PAComplianceItemResult
                    {
                        ItemId = operation.ElementId,
                        Success = false,
                        ErrorMessage = $"Unsupported operation type: {operation.OperationType}"
                    };
            }
        }

        /// <summary>
        /// Process family renames in batch
        /// </summary>
        private static List<PAComplianceItemResult> ProcessFamilyRenamesBatch(Document doc, List<PAElementModificationOperation> operations)
        {
            var results = new List<PAComplianceItemResult>();

            System.Diagnostics.Trace.WriteLine($"PA Element Modification: Starting batch rename of {operations.Count} families");

            using (var trans = new Transaction(doc, "Batch Rename Families"))
            {
                trans.Start();
                try
                {
                    foreach (var operation in operations)
                    {
                        try
                        {
                            System.Diagnostics.Trace.WriteLine($"  - Processing Element ID: '{operation.ElementId}', New Name: '{operation.NewValue}'");

                            if (!int.TryParse(operation.ElementId, out int idValue))
                            {
                                var errorMsg = $"Invalid Element ID format: '{operation.ElementId}'";
                                System.Diagnostics.Trace.WriteLine($"    ERROR: {errorMsg}");
                                results.Add(new PAComplianceItemResult
                                {
                                    ItemId = operation.ElementId,
                                    Success = false,
                                    ErrorMessage = errorMsg
                                });
                                continue;
                            }

                            var elementId = new ElementId((long)idValue);
                            var element = doc.GetElement(elementId);

                            if (element == null)
                            {
                                var errorMsg = $"Element not found with ID: {operation.ElementId}";
                                System.Diagnostics.Trace.WriteLine($"    ERROR: {errorMsg}");
                                results.Add(new PAComplianceItemResult
                                {
                                    ItemId = operation.ElementId,
                                    Success = false,
                                    ErrorMessage = errorMsg
                                });
                                continue;
                            }

                            System.Diagnostics.Trace.WriteLine($"    Found element: {element.GetType().Name} - '{element.Name}'");

                            var oldName = element.Name;
                            
                            // Check if it's a Family or FamilySymbol and handle appropriately
                            if (element is Family family)
                            {
                                System.Diagnostics.Trace.WriteLine($"    Renaming Family from '{family.Name}' to '{operation.NewValue}'");
                                family.Name = operation.NewValue;
                            }
                            else if (element is FamilySymbol familySymbol)
                            {
                                System.Diagnostics.Trace.WriteLine($"    Renaming FamilySymbol from '{familySymbol.Name}' to '{operation.NewValue}'");
                                familySymbol.Name = operation.NewValue;
                            }
                            else
                            {
                                System.Diagnostics.Trace.WriteLine($"    Renaming generic element from '{element.Name}' to '{operation.NewValue}'");
                                element.Name = operation.NewValue;
                            }

                            System.Diagnostics.Trace.WriteLine($"    SUCCESS: Renamed '{oldName}' to '{element.Name}'");

                            results.Add(new PAComplianceItemResult
                            {
                                ItemId = operation.ElementId,
                                ItemName = element.Name,
                                CurrentValue = oldName,
                                TargetValue = operation.NewValue,
                                Success = true,
                                OperationType = PAComplianceOperationType.Rename
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"    ERROR: Exception during rename: {ex.Message}");
                            results.Add(new PAComplianceItemResult
                            {
                                ItemId = operation.ElementId,
                                Success = false,
                                ErrorMessage = ex.Message,
                                OperationType = PAComplianceOperationType.Rename
                            });
                        }
                    }

                    trans.Commit();
                    System.Diagnostics.Trace.WriteLine($"PA Element Modification: Transaction committed successfully");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    System.Diagnostics.Trace.WriteLine($"PA Element Modification: Transaction rolled back due to error: {ex.Message}");
                    throw;
                }
            }

            return results;
        }

        /// <summary>
        /// Process sheet renames in batch
        /// </summary>
        private static List<PAComplianceItemResult> ProcessSheetRenamesBatch(Document doc, List<PAElementModificationOperation> operations)
        {
            var results = new List<PAComplianceItemResult>();

            System.Diagnostics.Trace.WriteLine($"PA Element Modification: Starting batch rename of {operations.Count} sheets");

            using (var trans = new Transaction(doc, "Batch Rename Sheets"))
            {
                trans.Start();
                try
                {
                    foreach (var operation in operations)
                    {
                        try
                        {
                            System.Diagnostics.Trace.WriteLine($"  - Processing Sheet Element ID: '{operation.ElementId}', New Name: '{operation.NewValue}'");

                            if (!int.TryParse(operation.ElementId, out int idValue))
                            {
                                var errorMsg = $"Invalid Element ID format: '{operation.ElementId}'";
                                System.Diagnostics.Trace.WriteLine($"    ERROR: {errorMsg}");
                                results.Add(new PAComplianceItemResult
                                {
                                    ItemId = operation.ElementId,
                                    Success = false,
                                    ErrorMessage = errorMsg
                                });
                                continue;
                            }

                            var elementId = new ElementId((long)idValue);
                            var element = doc.GetElement(elementId);
                            var sheet = element as ViewSheet;

                            if (sheet == null)
                            {
                                var errorMsg = element == null ? 
                                    $"Element not found with ID: {operation.ElementId}" : 
                                    $"Element with ID {operation.ElementId} is not a ViewSheet (it's a {element.GetType().Name})";
                                System.Diagnostics.Trace.WriteLine($"    ERROR: {errorMsg}");
                                results.Add(new PAComplianceItemResult
                                {
                                    ItemId = operation.ElementId,
                                    Success = false,
                                    ErrorMessage = errorMsg
                                });
                                continue;
                            }

                            System.Diagnostics.Trace.WriteLine($"    Found sheet: '{sheet.Name}' (Number: {sheet.SheetNumber})");

                            var oldName = sheet.Name;
                            sheet.Name = operation.NewValue;

                            System.Diagnostics.Trace.WriteLine($"    SUCCESS: Renamed sheet from '{oldName}' to '{sheet.Name}'");

                            results.Add(new PAComplianceItemResult
                            {
                                ItemId = operation.ElementId,
                                ItemName = sheet.Name,
                                CurrentValue = oldName,
                                TargetValue = operation.NewValue,
                                Success = true,
                                OperationType = PAComplianceOperationType.Rename
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"    ERROR: Exception during sheet rename: {ex.Message}");
                            results.Add(new PAComplianceItemResult
                            {
                                ItemId = operation.ElementId,
                                Success = false,
                                ErrorMessage = ex.Message,
                                OperationType = PAComplianceOperationType.Rename
                            });
                        }
                    }

                    trans.Commit();
                    System.Diagnostics.Trace.WriteLine($"PA Element Modification: Sheet transaction committed successfully");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    System.Diagnostics.Trace.WriteLine($"PA Element Modification: Sheet transaction rolled back due to error: {ex.Message}");
                    throw;
                }
            }

            return results;
        }

        /// <summary>
        /// Process workset renames in batch
        /// </summary>
        private static List<PAComplianceItemResult> ProcessWorksetRenamesBatch(Document doc, List<PAElementModificationOperation> operations)
        {
            var results = new List<PAComplianceItemResult>();

            if (!doc.IsWorkshared)
            {
                foreach (var operation in operations)
                {
                    results.Add(new PAComplianceItemResult
                    {
                        ItemId = operation.ElementId,
                        Success = false,
                        ErrorMessage = "Document is not workshared"
                    });
                }
                return results;
            }

            using (var trans = new Transaction(doc, "Batch Rename Worksets"))
            {
                trans.Start();
                try
                {
                    foreach (var operation in operations)
                    {
                        try
                        {
                            var worksetId = new WorksetId(int.Parse(operation.ElementId));
                            var worksetTable = doc.GetWorksetTable();
                            var workset = worksetTable.GetWorkset(worksetId);

                            if (workset == null)
                            {
                                results.Add(new PAComplianceItemResult
                                {
                                    ItemId = operation.ElementId,
                                    Success = false,
                                    ErrorMessage = "Workset not found"
                                });
                                continue;
                            }

                            WorksetTable.RenameWorkset(doc, worksetId, operation.NewValue);

                            results.Add(new PAComplianceItemResult
                            {
                                ItemId = operation.ElementId,
                                ItemName = workset.Name,
                                CurrentValue = workset.Name,
                                TargetValue = operation.NewValue,
                                Success = true,
                                OperationType = PAComplianceOperationType.Rename
                            });
                        }
                        catch (Exception ex)
                        {
                            results.Add(new PAComplianceItemResult
                            {
                                ItemId = operation.ElementId,
                                Success = false,
                                ErrorMessage = ex.Message,
                                OperationType = PAComplianceOperationType.Rename
                            });
                        }
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Represents a single element modification operation
    /// </summary>
    public class PAElementModificationOperation
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string CurrentValue { get; set; }
        public string NewValue { get; set; }
        public PAComplianceOperationType OperationType { get; set; }
    }
}