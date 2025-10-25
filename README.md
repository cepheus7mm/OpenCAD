# OpenCAD
A simple CAD application

## Getting Started

OpenCAD is a web-based CAD (Computer-Aided Design) application with a modern, intuitive interface.

### Running the Application

1. Open `index.html` in a web browser, or
2. Start a local web server in the project directory:
   ```bash
   python3 -m http.server 8080
   ```
   Then navigate to `http://localhost:8080` in your browser.

## Features

### Drawing Tools
- **Select Tool**: Click on objects to select and view/edit their properties
- **Line Tool**: Click and drag to draw lines
- **Rectangle Tool**: Click and drag to draw rectangles
- **Circle Tool**: Click at center and drag to set radius
- **Delete Tool**: Click on objects to delete them

### View Controls
- **Zoom In/Out**: Use the buttons or mouse wheel to zoom
- **Pan**: Hold Shift + Left Click or use Middle Mouse Button to pan
- **Fit to View**: Reset zoom and pan to default
- **Toggle Grid**: Show/hide the grid background

### Properties Panel
When an object is selected, you can:
- View object type and coordinates
- Change color
- Adjust line width

### File Operations
- **New**: Clear the canvas (with confirmation)
- **Save**: Export your drawing as JSON
- **Load**: Import a previously saved JSON file

## Technology Stack

- HTML5 Canvas for rendering
- Pure vanilla JavaScript (no dependencies)
- CSS3 for styling
- Modern dark theme inspired by VS Code
