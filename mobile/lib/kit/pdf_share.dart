import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:printing/printing.dart';

import '../core/api_client.dart';
import '../core/session.dart';
import '../theme/tokens.dart';

/// Hands a finished PDF to the phone: the system print dialog or the share sheet. Behind an interface so tests (and other
/// platforms) can swap it; the PDF itself is always built by the server, never on the device.
abstract class PdfOutput {
  Future<void> printPdf(Uint8List bytes, String fileName);
  Future<void> sharePdf(Uint8List bytes, String fileName);
}

/// [PdfOutput] using the `printing` plugin (Android print service / AirPrint, and the OS share sheet).
class PrintingPdfOutput implements PdfOutput {
  const PrintingPdfOutput();

  @override
  Future<void> printPdf(Uint8List bytes, String fileName) => Printing.layoutPdf(onLayout: (_) async => bytes, name: fileName);

  @override
  Future<void> sharePdf(Uint8List bytes, String fileName) => Printing.sharePdf(bytes: bytes, filename: fileName);
}

final pdfOutputProvider = Provider<PdfOutput>((ref) => const PrintingPdfOutput());

enum PdfAction { print, share }

/// App-bar button: asks Print or Share, downloads the server-rendered PDF from [path] and hands it to the OS.
/// Shows a spinner while downloading and a snackbar when the download or the hand-off fails.
class PrintShareButton extends ConsumerStatefulWidget {
  const PrintShareButton({super.key, required this.path, required this.fallbackFileName});

  /// API path of the PDF, for example `/api/v1/print/invoice/{id}/pdf`.
  final String path;

  /// Used when the server does not name the file.
  final String fallbackFileName;

  @override
  ConsumerState<PrintShareButton> createState() => _PrintShareButtonState();
}

class _PrintShareButtonState extends ConsumerState<PrintShareButton> {
  bool _busy = false;

  Future<PdfAction?> _choose() => showModalBottomSheet<PdfAction>(
        context: context,
        showDragHandle: true,
        builder: (ctx) => SafeArea(
          child: Padding(
            padding: const EdgeInsets.only(bottom: ErpSpace.s2),
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              ListTile(
                minTileHeight: 56,
                leading: const Icon(Icons.print_outlined),
                title: const Text('Print'),
                subtitle: const Text('Send the PDF to a printer'),
                onTap: () => Navigator.pop(ctx, PdfAction.print),
              ),
              ListTile(
                minTileHeight: 56,
                leading: const Icon(Icons.ios_share),
                title: const Text('Share PDF'),
                subtitle: const Text('Email, chat or save the file'),
                onTap: () => Navigator.pop(ctx, PdfAction.share),
              ),
            ]),
          ),
        ),
      );

  Future<void> _run() async {
    final action = await _choose();
    if (action == null || !mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    final output = ref.read(pdfOutputProvider);
    setState(() => _busy = true);
    try {
      final file = await ref.read(apiProvider).getBytes(widget.path);
      final name = file.fileName ?? widget.fallbackFileName;
      if (action == PdfAction.print) {
        await output.printPdf(file.bytes, name);
      } else {
        await output.sharePdf(file.bytes, name);
      }
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message), duration: const Duration(seconds: 6)));
    } catch (_) {
      messenger.showSnackBar(const SnackBar(content: Text("Couldn't open the print or share sheet."), duration: Duration(seconds: 6)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => IconButton(
        tooltip: _busy ? 'Preparing PDF' : 'Print / Share PDF',
        onPressed: _busy ? null : _run,
        icon: _busy
            ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2.2))
            : const Icon(Icons.print_outlined),
      );
}
