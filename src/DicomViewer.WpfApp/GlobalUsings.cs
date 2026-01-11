global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using DicomViewer.Application.Interfaces;
global using DicomViewer.Application.Services;
global using DicomViewer.Infrastructure.Dicom;
global using DicomViewer.Infrastructure.Pacs;
global using DicomViewer.ViewModels;
global using DicomViewer.WpfServices;

// System.Windows와 충돌 방지
// Prevent conflict with System.Windows
global using WpfApplication = System.Windows.Application;
