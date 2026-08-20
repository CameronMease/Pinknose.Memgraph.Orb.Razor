// Hands the browser a file the app produced in memory. Used for the SVG export, where the
// markup comes back from GetSvgAsync as a string with nowhere to go.
window.orbDemo = {
    download: (fileName, text) => {
        const url = URL.createObjectURL(new Blob([text], { type: "image/svg+xml" }));
        const link = document.createElement("a");

        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();

        // Revoked on the next tick: revoking immediately can cancel the download in some
        // browsers before it has read the blob.
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }
};
