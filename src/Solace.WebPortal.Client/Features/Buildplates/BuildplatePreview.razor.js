import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

export function initThreeJs(container, dataUri, size, isNight) {
    if (!container) return;

    dispose(container);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(isNight ? "#070066" : "#3B69F5");

    const ambientLight = new THREE.AmbientLight(0xffffff, 1.5);
    scene.add(ambientLight);

    const mainLight = new THREE.DirectionalLight(0xffffff, 2.5);
    mainLight.position.set(50, 100, 50);
    scene.add(mainLight);

    const fillLight = new THREE.DirectionalLight(0xffffff, 1.0);
    fillLight.position.set(-50, 50, -50);
    scene.add(fillLight);

    const camera = new THREE.PerspectiveCamera(75, container.clientWidth / container.clientHeight, 0.1, 10000);
    const cameraDistance = size * 1.25;
    camera.position.set(-cameraDistance, cameraDistance, -cameraDistance);
    camera.lookAt(0, 0, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.setPixelRatio(window.devicePixelRatio);
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 0, 0);
    controls.update();

    const loader = new GLTFLoader();
    loader.load(dataUri, (gltf) => {
        scene.add(gltf.scene);
    }, undefined, (error) => {
        console.error('Error loading 3D GLTF model:', error);
    });

    const state = {
        renderer,
        animationId: null,
        resizeObserver: null
    };

    const animate = function () {
        state.animationId = requestAnimationFrame(animate);
        controls.update();
        renderer.render(scene, camera);
    };
    animate();

    state.resizeObserver = new ResizeObserver(() => {
        if (container.clientWidth && container.clientHeight) {
            camera.aspect = container.clientWidth / container.clientHeight;
            camera.updateProjectionMatrix();
            renderer.setSize(container.clientWidth, container.clientHeight);
        }
    });
    state.resizeObserver.observe(container);

    container.__threeJsState = state;
}

export function dispose(container) {
    if (!container || !container.__threeJsState) return;

    const state = container.__threeJsState;
    
    if (state.animationId) {
        cancelAnimationFrame(state.animationId);
    }
    
    if (state.resizeObserver) {
        state.resizeObserver.disconnect();
    }
    
    if (state.renderer) {
        state.renderer.dispose();
        if (container.contains(state.renderer.domElement)) {
            container.removeChild(state.renderer.domElement);
        }
    }
    
    delete container.__threeJsState;
}

export function toggleFullscreen(element) {
    if (!document.fullscreenElement) {
        element.requestFullscreen().catch(err => {
            console.error(`Error attempting to enable full-screen mode: ${err.message}`);
        });
    } else {
        document.exitFullscreen();
    }
}
