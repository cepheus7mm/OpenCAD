// Canvas setup
const canvas = document.getElementById('canvas');
const ctx = canvas.getContext('2d');

// Set canvas size
canvas.width = 800;
canvas.height = 600;

// State
let currentTool = 'line';
let isDrawing = false;
let startX = 0;
let startY = 0;
let shapes = [];
let currentShape = null;

// Settings
let strokeColor = '#000000';
let lineWidth = 2;

// Tool buttons
const toolButtons = {
    line: document.getElementById('line-tool'),
    rectangle: document.getElementById('rectangle-tool'),
    circle: document.getElementById('circle-tool')
};

// Other elements
const colorPicker = document.getElementById('color-picker');
const lineWidthSlider = document.getElementById('line-width');
const widthValue = document.getElementById('width-value');
const clearBtn = document.getElementById('clear-btn');

// Tool selection
Object.keys(toolButtons).forEach(tool => {
    toolButtons[tool].addEventListener('click', () => {
        currentTool = tool;
        Object.values(toolButtons).forEach(btn => btn.classList.remove('active'));
        toolButtons[tool].classList.add('active');
    });
});

// Color picker
colorPicker.addEventListener('change', (e) => {
    strokeColor = e.target.value;
});

// Line width slider
lineWidthSlider.addEventListener('input', (e) => {
    lineWidth = parseInt(e.target.value);
    widthValue.textContent = lineWidth;
});

// Clear canvas
clearBtn.addEventListener('click', () => {
    shapes = [];
    redrawCanvas();
});

// Mouse events
canvas.addEventListener('mousedown', startDrawing);
canvas.addEventListener('mousemove', draw);
canvas.addEventListener('mouseup', stopDrawing);
canvas.addEventListener('mouseout', stopDrawing);

function startDrawing(e) {
    isDrawing = true;
    const rect = canvas.getBoundingClientRect();
    startX = e.clientX - rect.left;
    startY = e.clientY - rect.top;
    
    currentShape = {
        tool: currentTool,
        startX: startX,
        startY: startY,
        endX: startX,
        endY: startY,
        color: strokeColor,
        width: lineWidth
    };
}

function draw(e) {
    if (!isDrawing) return;
    
    const rect = canvas.getBoundingClientRect();
    const currentX = e.clientX - rect.left;
    const currentY = e.clientY - rect.top;
    
    currentShape.endX = currentX;
    currentShape.endY = currentY;
    
    redrawCanvas();
    drawShape(currentShape);
}

function stopDrawing() {
    if (isDrawing && currentShape) {
        shapes.push({...currentShape});
        currentShape = null;
    }
    isDrawing = false;
}

function drawShape(shape) {
    ctx.strokeStyle = shape.color;
    ctx.lineWidth = shape.width;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    
    ctx.beginPath();
    
    switch(shape.tool) {
        case 'line':
            ctx.moveTo(shape.startX, shape.startY);
            ctx.lineTo(shape.endX, shape.endY);
            ctx.stroke();
            break;
            
        case 'rectangle':
            const width = shape.endX - shape.startX;
            const height = shape.endY - shape.startY;
            ctx.strokeRect(shape.startX, shape.startY, width, height);
            break;
            
        case 'circle':
            const radius = Math.sqrt(
                Math.pow(shape.endX - shape.startX, 2) + 
                Math.pow(shape.endY - shape.startY, 2)
            );
            ctx.arc(shape.startX, shape.startY, radius, 0, 2 * Math.PI);
            ctx.stroke();
            break;
    }
}

function redrawCanvas() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    shapes.forEach(shape => drawShape(shape));
}

// Initial draw
redrawCanvas();
