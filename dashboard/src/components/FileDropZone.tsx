import React, { useCallback, useState } from 'react';

interface FileDropZoneProps {
  onSuccess: () => void;
}

export function FileDropZone({ onSuccess }: FileDropZoneProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback(async (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    setError(null);

    const files = e.dataTransfer.files;
    if (files.length === 0) return;

    const file = files[0];
    if (!file.name.endsWith('.json')) {
      setError('Please upload a .json snapshot file');
      return;
    }

    setIsLoading(true);

    try {
      const text = await file.text();
      
      // Post to API (relative path works because dashboard is served by API in Docker)
      // or configured proxy in dev
      const response = await fetch('/load', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: text
      });

      if (!response.ok) {
        let errorMessage = response.statusText;
        try {
          const errorData = await response.json();
          // ASP.NET Core Results.Problem returns 'detail', manual returns might use 'error'
          errorMessage = errorData.detail || errorData.error || errorData.title || errorMessage;
        } catch {
          // ignore json parse error, stick to statusText
        }
        throw new Error(`Upload failed: ${errorMessage}`);
      }

      onSuccess();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to upload snapshot');
    } finally {
      setIsLoading(false);
    }
  }, [onSuccess]);

  return (
    <div 
      className={`
        flex flex-col items-center justify-center p-12 rounded-xl border-2 border-dashed
        transition-all duration-200 cursor-pointer
        ${isDragging 
          ? 'border-cyan-500 bg-cyan-500/10 scale-105' 
          : 'border-slate-700 bg-slate-800/50 hover:border-slate-500 hover:bg-slate-800'}
      `}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <div className={`
        w-24 h-24 rounded-full flex items-center justify-center mb-8 transition-colors
        ${isDragging ? 'bg-cyan-500 text-white' : 'bg-slate-700 text-slate-400'}
      `}>
        {isLoading ? (
          <span className="material-symbols-outlined text-5xl animate-spin">sync</span>
        ) : (
          <span className="material-symbols-outlined text-5xl">cloud_upload</span>
        )}
      </div>

      <h3 className="text-2xl font-bold text-white mb-4">
        {isLoading ? 'Loading Snapshot...' : 'Drop Snapshot Here'}
      </h3>
      
      <p className="text-slate-400 text-center max-w-md mb-8 text-lg">
        Drag and drop a <code>snapshot.json</code> file to load it into the Cartographer.
      </p>

      {error && (
        <div className="flex items-center gap-2 text-red-400 bg-red-400/10 px-4 py-2 rounded-lg">
          <span className="material-symbols-outlined text-sm">error</span>
          <span className="text-sm">{error}</span>
        </div>
      )}
    </div>
  );
}
