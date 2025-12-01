using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Infrastructure.Services
{
    public class PdfReportGenerator : IPdfReportGenerator
    {
        private readonly IFileStorage _fileStorage;

        public PdfReportGenerator(IFileStorage fileStorage)
        {
            _fileStorage = fileStorage;
            // Configuración de licencia de QuestPDF (Community)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInspectionReport(Inspection inspection)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, inspection));
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        private void ComposeHeader(IContainer container)
        {
            var titleStyle = TextStyle.Default.FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Informe de Inspección").Style(titleStyle);
                    column.Item().Text(text =>
                    {
                        text.Span("Generado por: ");
                        text.Span("VisioAnalytica").SemiBold();
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Fecha: ");
                        text.Span($"{DateTime.Now:dd/MM/yyyy}");
                    });
                });

                // Aquí podrías poner el logo si lo tuvieras cargado
                // row.ConstantItem(100).Image(Placeholders.Image(100, 50));
            });
        }

        private void ComposeContent(IContainer container, Inspection inspection)
        {
            container.PaddingVertical(40).Column(column =>
            {
                column.Spacing(20);

                // 1. Resumen Ejecutivo
                column.Item().Element(c => ComposeExecutiveSummary(c, inspection));

                // 2. Detalles de Hallazgos
                column.Item().Element(c => ComposeFindingsDetails(c, inspection));
            });
        }

        private void ComposeExecutiveSummary(IContainer container, Inspection inspection)
        {
            container.Column(column =>
            {
                column.Item().Text("Resumen Ejecutivo").FontSize(16).SemiBold();
                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Cell().Element(CellStyle).Text("Empresa Cliente:");
                    table.Cell().Element(CellStyle).Text(inspection.AffiliatedCompany?.Name ?? "N/A").SemiBold();

                    table.Cell().Element(CellStyle).Text("Inspector:");
                    table.Cell().Element(CellStyle).Text(inspection.User?.FirstName + " " + inspection.User?.LastName).SemiBold();

                    table.Cell().Element(CellStyle).Text("Fecha de Inspección:");
                    table.Cell().Element(CellStyle).Text(inspection.StartedAt.ToString("dd/MM/yyyy HH:mm"));

                    table.Cell().Element(CellStyle).Text("Estado:");
                    table.Cell().Element(CellStyle).Text(inspection.Status).FontColor(GetStatusColor(inspection.Status));

                    static IContainer CellStyle(IContainer container)
                    {
                        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                    }
                });
            });
        }

        private void ComposeFindingsDetails(IContainer container, Inspection inspection)
        {
            container.Column(column =>
            {
                column.Item().Text("Detalle de Hallazgos").FontSize(16).SemiBold();
                
                var analyzedPhotos = inspection.Photos.Where(p => p.IsAnalyzed && p.AnalysisInspectionId.HasValue).ToList();

                if (!analyzedPhotos.Any())
                {
                    column.Item().Text("No se encontraron fotos analizadas en esta inspección.");
                    return;
                }

                foreach (var photo in analyzedPhotos)
                {
                    column.Item().PaddingTop(10).Element(c => ComposePhotoSection(c, photo));
                }
            });
        }

        private void ComposePhotoSection(IContainer container, Photo photo)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
            {
                // Intentar cargar la imagen
                try
                {
                    // Nota: Esto es síncrono porque QuestPDF lo requiere así en el callback.
                    // En un escenario real, deberíamos precargar las imágenes o usar un método asíncrono fuera del callback.
                    // Por simplicidad y dado que _fileStorage.ReadImageAsync es async, aquí haremos .Result (cuidado con deadlocks).
                    // Una mejor práctica sería cargar todas las imágenes antes de llamar a GeneratePdf.
                    var imageBytes = _fileStorage.ReadImageAsync(photo.ImageUrl).Result;
                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        column.Item().Height(200).Image(imageBytes).FitArea();
                    }
                    else
                    {
                        column.Item().Text("[Imagen no disponible]").FontColor(Colors.Red.Medium);
                    }
                }
                catch
                {
                    column.Item().Text("[Error al cargar imagen]").FontColor(Colors.Red.Medium);
                }

                column.Item().Text($"Capturada: {photo.CapturedAt:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Medium);

                // Hallazgos de esta foto
                if (photo.AnalysisInspection != null && photo.AnalysisInspection.Findings.Any())
                {
                    column.Item().PaddingTop(5).Text("Hallazgos Detectados:").SemiBold();
                    
                    foreach (var finding in photo.AnalysisInspection.Findings)
                    {
                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Column(findingColumn =>
                        {
                            findingColumn.Item().Text(finding.Description).FontSize(10);
                            findingColumn.Item().Text(text =>
                            {
                                text.Span("Riesgo: ").FontSize(9).FontColor(Colors.Grey.Darken2);
                                text.Span(finding.RiskLevel).FontSize(9).SemiBold().FontColor(GetRiskColor(finding.RiskLevel));
                            });
                            
                            if (!string.IsNullOrEmpty(finding.CorrectiveAction))
                            {
                                findingColumn.Item().Text($"Acción Correctiva: {finding.CorrectiveAction}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            }
                        });
                    }
                }
                else
                {
                    column.Item().PaddingTop(5).Text("No se detectaron hallazgos de riesgo.").FontSize(10).Italic().FontColor(Colors.Green.Medium);
                }
            });
        }

        private string GetRiskColor(string riskLevel)
        {
            return riskLevel?.ToLower() switch
            {
                "alto" or "high" or "crítico" => Colors.Red.Medium,
                "medio" or "medium" => Colors.Orange.Medium,
                "bajo" or "low" => Colors.Green.Medium,
                _ => Colors.Grey.Medium
            };
        }

        private string GetStatusColor(string status)
        {
            return status switch
            {
                "Completed" => Colors.Green.Medium,
                "Failed" => Colors.Red.Medium,
                "Analyzing" => Colors.Orange.Medium,
                _ => Colors.Grey.Medium
            };
        }
    }
}
