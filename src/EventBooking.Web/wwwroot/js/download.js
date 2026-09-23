// Generic client-side text-file download helper.
//
// Deliberately roster-agnostic so any future export reuses it unchanged. Blazor WebAssembly
// attaches its bearer token only to calls made through the app's HttpClient, so a plain anchor
// to an API route would download unauthenticated; the content is fetched in C# and saved here.
(function () {
    function saveTextFile(fileName, content) {
        var blob = new Blob([content ?? ""], { type: "text/csv;charset=utf-8" });
        var url = URL.createObjectURL(blob);
        var anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        anchor.style.display = "none";
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    }

    window.saveTextFile = saveTextFile;
})();
