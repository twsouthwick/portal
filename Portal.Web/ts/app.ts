/**
 * Portal.Web - Helper functions for Blazor components
 * This module exports utility functions for site launching and browser detection
 */

import '../css/app.css';
// Import blazor-reconnect for its side effects (MutationObserver setup)
/* webpackMode: "eager" */
import './blazor-reconnect';

// Export all helper functions
export * from './applaunch';
export * from './browserdetect';
export * from './storage';
