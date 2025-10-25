// OpenCAD - Development UI Application

class OpenCAD {
    constructor() {
        this.canvas = document.getElementById('cadCanvas');
        this.ctx = this.canvas.getContext('2d');
        this.objects = [];
        this.selectedObject = null;
        this.currentTool = 'select';
        this.isDrawing = false;
        this.startPoint = null;
        this.tempObject = null;
        this.showGrid = true;
        this.zoom = 1.0;
        this.panX = 0;
        this.panY = 0;
        this.isPanning = false;
        this.lastMousePos = null;

        this.init();
    }

    init() {
        this.setupCanvas();
        this.setupEventListeners();
        this.setupTools();
        this.render();
        window.addEventListener('resize', () => this.setupCanvas());
    }

    setupCanvas() {
        const container = this.canvas.parentElement;
        this.canvas.width = container.clientWidth;
        this.canvas.height = container.clientHeight;
        this.render();
    }

    setupEventListeners() {
        // Mouse events for drawing
        this.canvas.addEventListener('mousedown', (e) => this.handleMouseDown(e));
        this.canvas.addEventListener('mousemove', (e) => this.handleMouseMove(e));
        this.canvas.addEventListener('mouseup', (e) => this.handleMouseUp(e));
        this.canvas.addEventListener('wheel', (e) => this.handleWheel(e));

        // Mouse coordinates display
        this.canvas.addEventListener('mousemove', (e) => {
            const coords = this.getMousePos(e);
            document.getElementById('mouseCoords').textContent = 
                `X: ${Math.round(coords.x)}, Y: ${Math.round(coords.y)}`;
        });

        // View controls
        document.getElementById('zoomInBtn').addEventListener('click', () => this.zoomIn());
        document.getElementById('zoomOutBtn').addEventListener('click', () => this.zoomOut());
        document.getElementById('fitBtn').addEventListener('click', () => this.fitToView());
        document.getElementById('gridToggle').addEventListener('click', () => this.toggleGrid());

        // Header controls
        document.getElementById('newBtn').addEventListener('click', () => this.newDrawing());
        document.getElementById('saveBtn').addEventListener('click', () => this.saveDrawing());
        document.getElementById('loadBtn').addEventListener('click', () => this.loadDrawing());
    }

    setupTools() {
        const tools = ['select', 'line', 'rectangle', 'circle', 'delete'];
        tools.forEach(tool => {
            const btn = document.getElementById(`${tool}Tool`);
            if (btn) {
                btn.addEventListener('click', () => this.setTool(tool));
            }
        });
    }

    setTool(tool) {
        this.currentTool = tool;
        document.querySelectorAll('.tool-btn').forEach(btn => btn.classList.remove('active'));
        document.getElementById(`${tool}Tool`).classList.add('active');
        
        // Update cursor
        if (tool === 'select') {
            this.canvas.style.cursor = 'default';
        } else if (tool === 'delete') {
            this.canvas.style.cursor = 'not-allowed';
        } else {
            this.canvas.style.cursor = 'crosshair';
        }
    }

    getMousePos(e) {
        const rect = this.canvas.getBoundingClientRect();
        return {
            x: (e.clientX - rect.left - this.panX) / this.zoom,
            y: (e.clientY - rect.top - this.panY) / this.zoom
        };
    }

    handleMouseDown(e) {
        const pos = this.getMousePos(e);

        // Pan with middle mouse or space + left click
        if (e.button === 1 || (e.button === 0 && e.shiftKey)) {
            this.isPanning = true;
            this.lastMousePos = { x: e.clientX, y: e.clientY };
            this.canvas.style.cursor = 'grab';
            return;
        }

        if (this.currentTool === 'select') {
            this.selectedObject = this.findObjectAt(pos);
            this.updatePropertiesPanel();
            this.render();
        } else if (this.currentTool === 'delete') {
            const obj = this.findObjectAt(pos);
            if (obj) {
                this.objects = this.objects.filter(o => o !== obj);
                this.selectedObject = null;
                this.updatePropertiesPanel();
                this.render();
            }
        } else {
            this.isDrawing = true;
            this.startPoint = pos;
        }
    }

    handleMouseMove(e) {
        if (this.isPanning) {
            const dx = e.clientX - this.lastMousePos.x;
            const dy = e.clientY - this.lastMousePos.y;
            this.panX += dx;
            this.panY += dy;
            this.lastMousePos = { x: e.clientX, y: e.clientY };
            this.render();
            return;
        }

        if (!this.isDrawing) return;

        const pos = this.getMousePos(e);
        this.tempObject = this.createObject(this.currentTool, this.startPoint, pos);
        this.render();
    }

    handleMouseUp(e) {
        if (this.isPanning) {
            this.isPanning = false;
            this.canvas.style.cursor = this.currentTool === 'select' ? 'default' : 'crosshair';
            return;
        }

        if (!this.isDrawing) return;

        const pos = this.getMousePos(e);
        const obj = this.createObject(this.currentTool, this.startPoint, pos);
        
        if (obj) {
            this.objects.push(obj);
        }

        this.isDrawing = false;
        this.tempObject = null;
        this.render();
    }

    handleWheel(e) {
        e.preventDefault();
        const delta = e.deltaY > 0 ? 0.9 : 1.1;
        this.zoom *= delta;
        this.zoom = Math.max(0.1, Math.min(5, this.zoom));
        this.updateZoomDisplay();
        this.render();
    }

    createObject(type, start, end) {
        const obj = {
            type: type,
            color: '#4CAF50',
            lineWidth: 2
        };

        switch (type) {
            case 'line':
                obj.x1 = start.x;
                obj.y1 = start.y;
                obj.x2 = end.x;
                obj.y2 = end.y;
                break;
            case 'rectangle':
                obj.x = Math.min(start.x, end.x);
                obj.y = Math.min(start.y, end.y);
                obj.width = Math.abs(end.x - start.x);
                obj.height = Math.abs(end.y - start.y);
                break;
            case 'circle':
                obj.x = start.x;
                obj.y = start.y;
                obj.radius = Math.sqrt(
                    Math.pow(end.x - start.x, 2) + Math.pow(end.y - start.y, 2)
                );
                break;
            default:
                return null;
        }

        return obj;
    }

    findObjectAt(pos) {
        for (let i = this.objects.length - 1; i >= 0; i--) {
            const obj = this.objects[i];
            if (this.isPointInObject(pos, obj)) {
                return obj;
            }
        }
        return null;
    }

    isPointInObject(pos, obj) {
        const tolerance = 5 / this.zoom;
        
        switch (obj.type) {
            case 'line':
                return this.distanceToLine(pos, obj) < tolerance;
            case 'rectangle':
                return pos.x >= obj.x && pos.x <= obj.x + obj.width &&
                       pos.y >= obj.y && pos.y <= obj.y + obj.height;
            case 'circle':
                const dist = Math.sqrt(
                    Math.pow(pos.x - obj.x, 2) + Math.pow(pos.y - obj.y, 2)
                );
                return Math.abs(dist - obj.radius) < tolerance;
        }
        return false;
    }

    distanceToLine(point, line) {
        const { x1, y1, x2, y2 } = line;
        const A = point.x - x1;
        const B = point.y - y1;
        const C = x2 - x1;
        const D = y2 - y1;

        const dot = A * C + B * D;
        const lenSq = C * C + D * D;
        let param = -1;
        
        if (lenSq !== 0) param = dot / lenSq;

        let xx, yy;

        if (param < 0) {
            xx = x1;
            yy = y1;
        } else if (param > 1) {
            xx = x2;
            yy = y2;
        } else {
            xx = x1 + param * C;
            yy = y1 + param * D;
        }

        const dx = point.x - xx;
        const dy = point.y - yy;
        return Math.sqrt(dx * dx + dy * dy);
    }

    render() {
        // Clear canvas
        this.ctx.fillStyle = '#2d2d30';
        this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);

        this.ctx.save();
        this.ctx.translate(this.panX, this.panY);
        this.ctx.scale(this.zoom, this.zoom);

        // Draw grid
        if (this.showGrid) {
            this.drawGrid();
        }

        // Draw all objects
        this.objects.forEach(obj => {
            this.drawObject(obj, obj === this.selectedObject);
        });

        // Draw temporary object
        if (this.tempObject) {
            this.drawObject(this.tempObject, false, true);
        }

        this.ctx.restore();
    }

    drawGrid() {
        const gridSize = 50;
        const startX = Math.floor(-this.panX / this.zoom / gridSize) * gridSize;
        const startY = Math.floor(-this.panY / this.zoom / gridSize) * gridSize;
        const endX = startX + this.canvas.width / this.zoom + gridSize;
        const endY = startY + this.canvas.height / this.zoom + gridSize;

        this.ctx.strokeStyle = '#3e3e42';
        this.ctx.lineWidth = 1 / this.zoom;

        for (let x = startX; x < endX; x += gridSize) {
            this.ctx.beginPath();
            this.ctx.moveTo(x, startY);
            this.ctx.lineTo(x, endY);
            this.ctx.stroke();
        }

        for (let y = startY; y < endY; y += gridSize) {
            this.ctx.beginPath();
            this.ctx.moveTo(startX, y);
            this.ctx.lineTo(endX, y);
            this.ctx.stroke();
        }
    }

    drawObject(obj, isSelected = false, isTemp = false) {
        this.ctx.strokeStyle = isTemp ? '#888' : obj.color;
        this.ctx.lineWidth = (obj.lineWidth || 2) / this.zoom;

        if (isSelected) {
            this.ctx.strokeStyle = '#FFD700';
            this.ctx.lineWidth = 3 / this.zoom;
        }

        switch (obj.type) {
            case 'line':
                this.ctx.beginPath();
                this.ctx.moveTo(obj.x1, obj.y1);
                this.ctx.lineTo(obj.x2, obj.y2);
                this.ctx.stroke();
                break;
            case 'rectangle':
                this.ctx.strokeRect(obj.x, obj.y, obj.width, obj.height);
                break;
            case 'circle':
                this.ctx.beginPath();
                this.ctx.arc(obj.x, obj.y, obj.radius, 0, Math.PI * 2);
                this.ctx.stroke();
                break;
        }
    }

    updatePropertiesPanel() {
        const panel = document.getElementById('propertiesContent');
        
        if (!this.selectedObject) {
            panel.innerHTML = '<p class="no-selection">No object selected</p>';
            return;
        }

        const obj = this.selectedObject;
        let html = `<div class="property-group">
            <label class="property-label">Type</label>
            <input type="text" class="property-input" value="${obj.type}" readonly>
        </div>
        <div class="property-group">
            <label class="property-label">Color</label>
            <input type="color" class="property-color" id="objColor" value="${obj.color}">
        </div>
        <div class="property-group">
            <label class="property-label">Line Width</label>
            <input type="number" class="property-input" id="objLineWidth" value="${obj.lineWidth}" min="1" max="10">
        </div>`;

        switch (obj.type) {
            case 'line':
                html += `<div class="property-group">
                    <label class="property-label">Start X</label>
                    <input type="number" class="property-input" value="${Math.round(obj.x1)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">Start Y</label>
                    <input type="number" class="property-input" value="${Math.round(obj.y1)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">End X</label>
                    <input type="number" class="property-input" value="${Math.round(obj.x2)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">End Y</label>
                    <input type="number" class="property-input" value="${Math.round(obj.y2)}" readonly>
                </div>`;
                break;
            case 'rectangle':
                html += `<div class="property-group">
                    <label class="property-label">X</label>
                    <input type="number" class="property-input" value="${Math.round(obj.x)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">Y</label>
                    <input type="number" class="property-input" value="${Math.round(obj.y)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">Width</label>
                    <input type="number" class="property-input" value="${Math.round(obj.width)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">Height</label>
                    <input type="number" class="property-input" value="${Math.round(obj.height)}" readonly>
                </div>`;
                break;
            case 'circle':
                html += `<div class="property-group">
                    <label class="property-label">Center X</label>
                    <input type="number" class="property-input" value="${Math.round(obj.x)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">Center Y</label>
                    <input type="number" class="property-input" value="${Math.round(obj.y)}" readonly>
                </div>
                <div class="property-group">
                    <label class="property-label">Radius</label>
                    <input type="number" class="property-input" value="${Math.round(obj.radius)}" readonly>
                </div>`;
                break;
        }

        panel.innerHTML = html;

        // Add event listeners for editable properties
        const colorInput = document.getElementById('objColor');
        if (colorInput) {
            colorInput.addEventListener('change', (e) => {
                obj.color = e.target.value;
                this.render();
            });
        }

        const lineWidthInput = document.getElementById('objLineWidth');
        if (lineWidthInput) {
            lineWidthInput.addEventListener('change', (e) => {
                obj.lineWidth = parseInt(e.target.value);
                this.render();
            });
        }
    }

    zoomIn() {
        this.zoom *= 1.2;
        this.zoom = Math.min(5, this.zoom);
        this.updateZoomDisplay();
        this.render();
    }

    zoomOut() {
        this.zoom *= 0.8;
        this.zoom = Math.max(0.1, this.zoom);
        this.updateZoomDisplay();
        this.render();
    }

    fitToView() {
        this.zoom = 1.0;
        this.panX = 0;
        this.panY = 0;
        this.updateZoomDisplay();
        this.render();
    }

    toggleGrid() {
        this.showGrid = !this.showGrid;
        this.render();
    }

    updateZoomDisplay() {
        document.getElementById('zoomLevel').textContent = 
            `Zoom: ${Math.round(this.zoom * 100)}%`;
    }

    newDrawing() {
        if (this.objects.length > 0) {
            if (confirm('Clear current drawing?')) {
                this.objects = [];
                this.selectedObject = null;
                this.updatePropertiesPanel();
                this.render();
            }
        }
    }

    saveDrawing() {
        const data = JSON.stringify({
            objects: this.objects,
            version: '1.0'
        });
        const blob = new Blob([data], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'opencad-drawing.json';
        a.click();
        URL.revokeObjectURL(url);
    }

    loadDrawing() {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.json';
        input.onchange = (e) => {
            const file = e.target.files[0];
            const reader = new FileReader();
            reader.onload = (event) => {
                try {
                    const data = JSON.parse(event.target.result);
                    this.objects = data.objects || [];
                    this.selectedObject = null;
                    this.updatePropertiesPanel();
                    this.render();
                } catch (err) {
                    alert('Failed to load drawing: ' + err.message);
                }
            };
            reader.readAsText(file);
        };
        input.click();
    }
}

// Initialize the application
let app;
window.addEventListener('DOMContentLoaded', () => {
    app = new OpenCAD();
});
